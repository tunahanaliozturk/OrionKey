# OrionKey Benchmarks

A [BenchmarkDotNet](https://benchmarkdotnet.org/) suite measuring the pure-CPU hot paths of OrionKey:
id generation, parsing, formatting, and the equality / comparison surface. Nothing here touches a
database, the network, or any external service, so the numbers reflect the library itself rather than
I/O.

The project lives at `benchmarks/Moongazing.OrionKey.Benchmarks`. It builds as a net10.0 host that
spawns net8.0 and net9.0 child processes per run, so every benchmark reports both runtimes side by
side (via `[SimpleJob(RuntimeMoniker.Net80)]` and `[SimpleJob(RuntimeMoniker.Net90)]`). Every class is
a `[MemoryDiagnoser]`, so allocations and GC counts are reported alongside time.

This document describes what each benchmark measures and why. It deliberately contains no specific
nanosecond or allocation figures: those depend entirely on your CPU, runtime, and build, and quoting
fixed numbers here would be misleading. Run the suite locally to get figures for your hardware.

## Benchmark fixtures

Three strongly-typed ids are declared in the benchmark project (`BenchmarkIds.cs`), one per backing
kind, so the suite exercises a representative spread without depending on the sample project:

- `OrderId` - `Guid`-backed, default strategy (`Guid.NewGuid()` under the hood).
- `UserId` - `long`-backed, Snowflake strategy (zero-allocation, time-sortable).
- `TenantId` - `string`-backed, ULID strategy (26-character canonical form, sortable).

The Snowflake worker id is pinned once via `OrionKeyBootstrap.EnsureConfigured()` (called from each
`[GlobalSetup]`) so generation is deterministic and never falls back to the machine-name-hash worker
id during a measured run.

## Classes

### `RawGenerationBenchmarks`

Per-strategy cost of minting one raw id through the `OrionKey` facade: `NextSnowflake`, `NewGuidV7`,
`NewSequentialGuid`, `NewUlid`, `NewNanoId`, `NewObjectId`, `NewKsuid`, `NewCuid2`. `Guid.NewGuid()` is
the `[Benchmark(Baseline = true)]`, since it is the cheapest identifier .NET offers. The ratio column
then answers a concrete question for each strategy: what does sortability, smaller storage, or a
particular textual format cost relative to a plain GUID? The `MemoryDiagnoser` column distinguishes
the zero-allocation numeric / GUID strategies from the string strategies, which must allocate their
textual form.

### `TypedIdGenerationBenchmarks`

Isolates the overhead the generated strongly-typed wrapper adds over calling the underlying generator
directly. Each backing kind is measured both ways: `OrderId.New()` against `Guid.NewGuid()`,
`UserId.New()` against `OrionKey.NextSnowflake()`, and `TenantId.New()` against `OrionKey.NewUlid()`.
The wrapper is a `readonly struct` whose `New()` just forwards to the strategy, so the expectation is
that the typed and raw rows are statistically indistinguishable; the point of the benchmark is to
verify that claim rather than assert it. It also measures the generated `CreateMany(n)` bulk helper,
which pre-allocates the result array and avoids LINQ at the call site.

### `ParseBenchmarks`

The inbound path: turning a string or span back into a typed id, as happens during route binding and
deserialization. For the GUID backing it compares a hand-written naive parse
(`new OrderId(Guid.Parse(text))`, the baseline) against the generated `Parse(string)`,
`Parse(ReadOnlySpan<char>)`, and `TryParse`. The span overload exists so HTTP routing can bind without
first materializing a string, and this benchmark is where that saving (or its absence) shows up. The
Snowflake (`long`) and ULID (`string`) backings are covered too, including a span `TryParse`.

### `FormatEqualityBenchmarks`

The outbound and in-memory surface that EF Core change tracking, dictionary keying, and serialization
hit on every request: `ToString`, allocation-free UTF-8 `TryFormat`, `Equals`, `GetHashCode`, and
`CompareTo`. The UTF-8 baseline is "`ToString()` then UTF-8 encode", compared against the direct
`TryFormat(Span<byte>, ...)` path that System.Text.Json takes, so the ratio shows what writing bytes
directly saves over routing through an intermediate string. `SnowflakeCompareTo` exercises the
numeric, time-ordered comparison; `GuidCompareTo` exercises the GUID comparison.

## Running

```bash
dotnet run -c Release --project benchmarks/Moongazing.OrionKey.Benchmarks
```

Run a single class or a filtered subset with the standard BenchmarkDotNet switches:

```bash
# one class
dotnet run -c Release --project benchmarks/Moongazing.OrionKey.Benchmarks -- --filter '*RawGenerationBenchmarks*'

# everything
dotnet run -c Release --project benchmarks/Moongazing.OrionKey.Benchmarks -- --filter '*'
```

Results are written under `BenchmarkDotNet.Artifacts/results/` (gitignored). Always run in `Release`;
BenchmarkDotNet refuses to produce trustworthy numbers from a `Debug` build.

## Reading the results

- `Guid.NewGuid()` is the floor. Everything slower is paying for sortability, smaller storage, or a
  specific textual format. Decide whether that trade is worth it for your workload.
- Watch the `Allocated` column. Numeric and GUID strategies (Snowflake, GuidV7, SequentialGuid) are
  allocation-free; the string strategies (ULID, NanoId, ObjectId, KSUID, CUID2) allocate their string
  form. Under high throughput that allocation, not the raw compute, is usually what matters.
- `Ratio` near 1.00 in `TypedIdGenerationBenchmarks` confirms the strongly-typed wrapper is free.
- A database identity column is not benchmarked here: its cost is dominated by the insert round-trip,
  not id generation, so it is not comparable to these in-process paths.
