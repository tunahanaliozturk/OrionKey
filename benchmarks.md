# OrionKey Benchmarks

Latest run: 2026-05-22 on Intel Core i7-7820HQ CPU @ 2.90 GHz (Kaby Lake, 4 physical / 8 logical cores), Windows 11 22H2, .NET 10.0.5, BenchmarkDotNet 0.14.0.

> **Note.** These numbers are reference-grade, not marketing claims. Reproduce locally with `dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks`. Your hardware will differ.

## Methodology

- BenchmarkDotNet `DefaultJob` (the `--job short` switch was passed but the 0.14.0 runner ran `DefaultJob` regardless; full run took roughly 4 minutes and produced statistically stable numbers).
- Memory profiler enabled (`[MemoryDiagnoser]`).
- `Guid.NewGuid()` is the ratio baseline (`Ratio` column).
- All allocations and GC stats reported.
- Each scenario is a single `New()` call; no shared state between runs.

```text
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22621.4317/22H2/2022Update/SunValley2)
Intel Core i7-7820HQ CPU 2.90GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
```

## Scenarios

### Single id generation, per strategy

| Method         |        Mean |     Error |    StdDev | Ratio | RatioSD |   Gen0 | Allocated |
|----------------|------------:|----------:|----------:|------:|--------:|-------:|----------:|
| RawGuid        |    70.19 ns |  0.611 ns |  0.510 ns |  1.00 |    0.01 |      - |       0 B |
| ObjectId       |    87.82 ns |  1.449 ns |  1.355 ns |  1.25 |    0.02 | 0.0343 |     144 B |
| Ulid           |   101.56 ns |  2.107 ns |  4.398 ns |  1.45 |    0.06 | 0.0191 |      80 B |
| GuidV7         |   121.92 ns |  2.315 ns |  2.377 ns |  1.74 |    0.04 |      - |       0 B |
| SequentialGuid |   144.65 ns |  2.901 ns |  5.305 ns |  2.06 |    0.08 |      - |       0 B |
| NanoId         |   147.43 ns |  2.919 ns |  3.475 ns |  2.10 |    0.05 | 0.0153 |      64 B |
| Snowflake      |   241.43 ns |  0.073 ns |  0.068 ns |  3.44 |    0.02 |      - |       0 B |
| Ksuid          | 1,510.77 ns | 29.633 ns | 45.253 ns | 21.53 |    0.65 | 0.2270 |     952 B |
| Cuid2          | 3,480.20 ns | 69.405 ns | 99.539 ns | 49.59 |    1.44 | 0.0153 |      72 B |

Interpretation:

- `Guid.NewGuid()` is the floor; everything else pays for sortability, smaller storage, or a particular textual format.
- `Snowflake`, `GuidV7`, and `SequentialGuid` allocate zero bytes. They are the right defaults when you want time-sortability without per-id GC pressure. The Snowflake cost (about 3.4x baseline) is dominated by the spinwait that protects sequence rollover inside the same millisecond; under realistic load it amortizes away.
- String strategies (`Ulid`, `NanoId`, `Cuid2`, `Ksuid`, `ObjectId`) allocate their string storage. The 80 B for ULID is exactly the 26-character canonical form rounded up to .NET's string overhead.
- `Cuid2` and `Ksuid` are the slowest by an order of magnitude because they rely on cryptographic hashing and base32 encoding respectively. Reach for them only when you specifically need their guarantees (CUID2's collision resistance for distributed clients, KSUID's k-sortable lexicographic ordering with secondary random bits).

### What is NOT measured yet

- Strongly-typed wrapper overhead (calling `OrderId.New()` vs. calling the strategy directly). The generated wrapper is `[MethodImpl(AggressiveInlining)]` so it should be a no-op, but a confirming benchmark has not been added yet.
- EF Core value-converter cost on the hot read path.
- `JsonConverter` and `ISpanParsable` write/parse cost.
- Throughput under contention (16 / 64 concurrent producers).

These are on the v0.5.0 stabilization milestone.

## How to reproduce

```bash
cd <repo-root>
dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks
```

Results appear in `BenchmarkDotNet.Artifacts/results/`.

## Comparison baselines

- **`Guid.NewGuid()`** is the ratio baseline above. It is the closest thing .NET has to a "free" identifier and the right thing to compare against when deciding whether time-sortability is worth the extra nanoseconds.
- **`IdentitySerial`** (a database identity column) is not benchmarked here because the cost is dominated by the round-trip to the database, not by id generation. If you can tolerate "id assigned at insert time" semantics, `IdentitySerial` beats every strategy in this file on raw per-id cost.
- **External UUID libraries.** The numbers above include `Ulid`, `NanoId`, `Cuid2`, `Ksuid`, and `ObjectId` implementations sourced from the most popular community packages so the comparison is honest rather than synthetic.

The point of the comparison is to be honest about where each strategy sits. If a particular strategy turns out to be measurably slower than its upstream reference, we will say so and investigate.
