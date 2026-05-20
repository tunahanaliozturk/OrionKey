<p align="center">
  <img src="docs/logo.png" alt="OrionKey" width="160" />
</p>

<h1 align="center">OrionKey</h1>

<p align="center">Source-generated strongly-typed IDs for .NET.</p>

<p align="center">
  <a href="https://www.nuget.org/packages/OrionKey"><img src="https://img.shields.io/nuget/v/OrionKey?style=flat-square&color=blue" alt="NuGet" /></a>
  <a href="https://www.nuget.org/packages/OrionKey"><img src="https://img.shields.io/nuget/dt/OrionKey?style=flat-square&color=green" alt="Downloads" /></a>
  <img src="https://img.shields.io/badge/license-MIT-yellow?style=flat-square" alt="License" />
  <img src="https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple?style=flat-square" alt="Target" />
</p>

OrionKey turns a `readonly partial struct` into a fully-featured strongly-typed ID with a
single attribute. A bundled Roslyn source generator emits the equality, comparison, factory,
serialization, and persistence members, so a domain ID stops being a bare `Guid` or `long`
and becomes a distinct type the compiler can check. There is no base class, no runtime
reflection, and nothing to wire up: declare the struct, build, use it.

## Quick start

```
dotnet add package OrionKey
```

Declare an ID by marking a partial struct with `[OrionId]` and a storage type. The optional
second type argument selects a generation strategy:

```csharp
[OrionId<Guid>]              public readonly partial struct OrderId;
[OrionId<long, Snowflake>]   public readonly partial struct UserId;
[OrionId<string, Ulid>]      public readonly partial struct TenantId;
[OrionId<string, NanoId>]    public readonly partial struct SessionId;
```

Each struct now has a `New()` factory, value equality, parsing, and serialization:

```csharp
var id = OrderId.New();

OrderId a = OrderId.New();
OrderId b = a;
Console.WriteLine(a == b);   // true, value equality

// Works as-is with System.Text.Json
var json = JsonSerializer.Serialize(new { OrderId = id });

// Works as-is as an EF Core key
public DbSet<Order> Orders { get; set; }   // Order.Id is an OrderId

// Works as-is as a minimal-API route parameter
app.MapGet("/orders/{id}", (OrderId id) => /* ... */);
```

The generated converters are discovered automatically by `System.Text.Json`, EF Core, and
ASP.NET Core model binding. No manual registration is required.

## Strategies

| Declaration | Storage | New() | Sortable |
|---|---|---|---|
| `[OrionId<Guid>]` | Guid | `Guid.NewGuid()` | no |
| `[OrionId<Guid, GuidV7>]` | Guid | UUIDv7 | yes |
| `[OrionId<long, Snowflake>]` | long | Snowflake | yes |
| `[OrionId<string, Ulid>]` | string | ULID | yes |
| `[OrionId<string, NanoId>]` | string | NanoId | no |
| `[OrionId<int>]` / `[OrionId<long>]` | int/long | none (DB identity) | n/a |

The `int` and `long` integer forms have no `New()` factory; they model ids assigned
externally, typically by a database identity column.

## What gets generated

For every annotated struct the generator emits, as `partial` companions:

- The struct body itself: a `Value` member, a `New()` factory (strategy-backed types), and
  value-based `IEquatable` equality with `==` / `!=`.
- An `IComparable` / `IComparable<T>` implementation, emitted only for sortable strategies
  (`GuidV7`, `Snowflake`, `Ulid`).
- A `System.Text.Json` `JsonConverter` so the id serializes as its underlying value.
- A `TypeConverter` for framework conversions and ASP.NET Core model binding.
- `IParsable<T>` and `ISpanParsable<T>` implementations for allocation-aware parsing.
- An EF Core `ValueConverter`, emitted only when the project references EF Core, so the id
  can be used directly as an entity key or property.

## Snowflake worker IDs

Snowflake ids embed a per-process **worker ID** (10 bits, 0-1023) to stay unique across
instances. Configure it explicitly with
`OrionKey.Configure(o => o.SnowflakeWorkerId = N)` or the `ORIONKEY_WORKER_ID` environment
variable; otherwise OrionKey derives one from the machine name and writes a one-time
warning. In any multi-instance deployment you should pin the worker ID explicitly. See
[docs/snowflake-worker-ids.md](docs/snowflake-worker-ids.md) for details.

## Benchmarks

Id generation throughput, measured with BenchmarkDotNet. The `--job short` switch was
passed but BenchmarkDotNet 0.14.0 ran the `DefaultJob` regardless; the full run still
completed in roughly 2.5 minutes and produced the statistically stable numbers below.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22621.4317/22H2/2022Update/SunValley2)
Intel Core i7-7820HQ CPU 2.90GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
```

| Method    | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| RawGuid   |  82.55 ns | 1.652 ns | 1.464 ns |  1.00 |    0.02 |      - |         - |          NA |
| Snowflake | 241.02 ns | 0.093 ns | 0.087 ns |  2.92 |    0.05 |      - |         - |          NA |
| Ulid      |  99.63 ns | 2.039 ns | 3.233 ns |  1.21 |    0.04 | 0.0191 |      80 B |          NA |
| NanoId    | 153.30 ns | 3.089 ns | 5.725 ns |  1.86 |    0.08 | 0.0153 |      64 B |          NA |
| GuidV7    | 118.11 ns | 2.372 ns | 3.964 ns |  1.43 |    0.05 |      - |         - |          NA |

`RawGuid` (`Guid.NewGuid()`) is the baseline. `Snowflake` and `GuidV7` allocate nothing;
`Ulid` and `NanoId` allocate their string storage. Reproduce with
`dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks`.

## Testing

The `OrionKey.Testing` package makes generated ids predictable in tests. A
`DeterministicIdScope` overrides the active generators for its lifetime, and the bundled
sequential generators hand out ascending, repeatable ids so assertions do not depend on
random or time-based values. Wrap the code under test in a scope and the ids it mints
become deterministic.

## More from the Orion family

OrionKey is one of a set of standalone .NET libraries:

- [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) - guard clauses, validation, DDD primitives for .NET.
- [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) - automatic EF Core change-audit trail.

## License

OrionKey is released under the MIT License. See [LICENSE.txt](LICENSE.txt).
