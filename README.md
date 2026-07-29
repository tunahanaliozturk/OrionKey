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

## How it works

A 64-bit Snowflake id packs three fields into a `long`: a millisecond timestamp relative to a
fixed epoch, a per-process worker id, and a per-millisecond sequence counter. The layout is
what makes Snowflake ids time-sortable, unique across instances without coordination, and free
of allocation.

```mermaid
flowchart LR
    Sign["sign<br/>1 bit<br/>(always 0)"] --> Ts["timestamp<br/>41 bits<br/>ms since epoch"]
    Ts --> Worker["worker id<br/>10 bits<br/>0..1023"]
    Worker --> Seq["sequence<br/>12 bits<br/>0..4095 per ms"]

    classDef fixed fill:#dbeafe,stroke:#1e40af,color:#1e3a8a
    classDef tunable fill:#fce7f3,stroke:#9d174d,color:#831843
    class Sign,Ts fixed
    class Worker,Seq tunable
```

Worker id is the only field that needs human attention; the timestamp comes from the clock and
the sequence is internal. Pin it with `OrionKey.Configure(o => o.SnowflakeWorkerId = N)` or
the `ORIONKEY_WORKER_ID` environment variable in every replica.

OrionKey also ships an idempotency-claim helper used by the OrionShowcase MediatR
`IdempotencyBehavior`. A command that carries an `IdempotencyKey` first asks the store
whether a previous response exists for that key, then proceeds only on a fresh claim. The
key id itself is OrionKey-generated so it sorts naturally.

```mermaid
sequenceDiagram
    autonumber
    participant App as Application code
    participant Beh as IdempotencyBehavior
    participant Store as IIdempotencyStore<br/>(OrionKey-backed)
    participant Hnd as Command handler

    App->>Beh: Send(cmd, idempotencyKey)
    Beh->>Store: TryClaimAsync(key)
    alt fresh claim
        Store-->>Beh: claimed (new OrionKey id)
        Beh->>Hnd: invoke
        Hnd-->>Beh: response
        Beh->>Store: StoreResponseAsync(key, json)
        Beh-->>App: response
    else replay
        Store-->>Beh: stored response json
        Beh-->>App: replay (no handler call)
    end
```

## Quick start

```shell
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
| --- | --- | --- | --- |
| `[OrionId<Guid>]` | Guid | `Guid.NewGuid()` | no |
| `[OrionId<Guid, GuidV7>]` | Guid | UUIDv7 | yes |
| `[OrionId<Guid, SequentialGuid>]` | Guid | SQL Server-ordered sequential GUID | yes |
| `[OrionId<long, Snowflake>]` | long | Snowflake | yes |
| `[OrionId<string, Ulid>]` | string | ULID | yes |
| `[OrionId<string, Ksuid>]` | string | KSUID | yes |
| `[OrionId<string, ObjectId>]` | string | MongoDB ObjectId (24-char hex) | yes |
| `[OrionId<string, MonotonicHex>]` | string | 32-char lowercase hex, time-ordered | yes |
| `[OrionId<string, NanoId>]` | string | NanoId | no |
| `[OrionId<string, Cuid2>]` | string | CUID2 | no |
| `[OrionId<int>]` / `[OrionId<long>]` | int/long | none (DB identity) | n/a |

The `int` and `long` integer forms have no `New()` factory; they model ids assigned
externally, typically by a database identity column.

### MonotonicHex

`MonotonicHex` produces a 32-character lowercase hex string built from a 48-bit millisecond
Unix timestamp (big-endian) followed by 80 bits of randomness. The big-endian layout and the
`0-9a-f` alphabet make ordinal string comparison equal chronological order, so the raw string
sorts the same way in code, in a database index, and in a hex-sorted store. Ids minted within
the same millisecond are strictly increasing within a process: the randomness block is
incremented rather than re-drawn, so a sequence is monotonic. If the clock steps backwards
(NTP correction, leap second) the previous timestamp is held so the sequence never regresses.

```csharp
[OrionId<string, MonotonicHex>] public readonly partial struct TraceId;

var a = TraceId.New();
var b = TraceId.New();

// Ordinal order equals creation order; b was minted after a.
Console.WriteLine(string.CompareOrdinal(a.Value, b.Value) < 0);   // true
```

The runtime factory is also exposed directly for code that needs a raw id without a wrapper
struct: `OrionKey.NewMonotonicHex()` returns the same 32-char lowercase hex string.

## What gets generated

For every annotated struct the generator emits, as `partial` companions:

- The struct body itself: a `Value` member, a `New()` factory (strategy-backed types), and
  value-based `IEquatable` equality with `==` / `!=`.
- An `IComparable` / `IComparable<T>` implementation, emitted only for sortable strategies
  (`GuidV7`, `SequentialGuid`, `Snowflake`, `Ulid`, `Ksuid`, `ObjectId`, `MonotonicHex`).
- A `System.Text.Json` `JsonConverter` so the id serializes as its underlying value.
- A `TypeConverter` for framework conversions and ASP.NET Core model binding.
- `IParsable<T>` and `ISpanParsable<T>` implementations for allocation-aware parsing.
- An EF Core `ValueConverter`, emitted only when the project references EF Core, so the id
  can be used directly as an entity key or property.

## Library integration

OrionKey emits additional companions automatically when the consumer project references any of these libraries. There is no extra configuration — the source generator probes the compilation and only emits what the project can use.

| Library | Generated companion | One-line registration |
| --- | --- | --- |
| System.Text.Json (source-gen) | `OrionKeyJsonConverterFactory` | `OrionKeyJsonRegistrar.AddTo(options);` |
| Dapper | `<Id>DapperTypeHandler` | `OrionKeyDapperRegistrar.Register();` |
| Newtonsoft.Json | `<Id>NewtonsoftJsonConverter` | `OrionKeyNewtonsoftJsonRegistrar.AddTo(settings);` |
| MongoDB driver | `<Id>BsonSerializer` | `OrionKeyMongoRegistrar.Register();` |
| Swashbuckle (OpenAPI) | `<Id>SchemaFilter` | `OrionKeyOpenApiRegistrar.AddTo(options);` |

Each registrar enumerates every `[OrionId]` struct in the assembly and wires it into the library's registry, so a single call covers every id you have declared.

For ordinary reflection-based `System.Text.Json`, EF Core, and ASP.NET Core model binding, the generated companions are still auto-discovered via attributes / conventions, so no registrar call is required. The `OrionKeyJsonRegistrar.AddTo(options)` call exists for the reflection-free source-generation path: pair it with a `JsonSerializerContext` constructed over those options, as covered in [AOT & trimming](#aot--trimming) below.

### System.Text.Json source-generation

`OrionKeyJsonRegistrar.AddTo(JsonSerializerOptions)` registers every generated id converter on the supplied options in one call, backed by `OrionKeyJsonConverterFactory`, a reflection-free `JsonConverterFactory` that resolves the assembly's `[OrionId]` types through a compile-time type switch. This is the path to use with a source-generated `JsonSerializerContext`, where the per-id `[JsonConverter]` attribute is not visible to the `System.Text.Json` generator. Register the converters first, then construct the context over those same options:

```csharp
var options = new JsonSerializerOptions();
OrionKeyJsonRegistrar.AddTo(options);     // wires every [OrionId] converter, AOT-safe
var ctx = new MyJsonContext(options);     // construct the context over those options

var json = JsonSerializer.Serialize(OrderId.New(), ctx.OrderId);
var id = JsonSerializer.Deserialize(json, ctx.OrderId);
```

Constructing the context with a bare `MyJsonContext.Default` does not honor the converters, because `Default` is built over an internal options instance that never saw the `AddTo` call. Always build the context over the options you registered into. Ids still serialize as their bare scalar (string or number) shape.

## AOT & trimming

OrionKey is compatible with Native AOT (`<PublishAot>true</PublishAot>`) and trimming (`<PublishTrimmed>true</PublishTrimmed>`). Both runtime assemblies (`OrionKey`, `OrionKey.Testing`) carry `<IsAotCompatible>true</IsAotCompatible>`, every generated converter is reachable via attributes (no runtime reflection scan), and CI publishes a self-contained AOT sample binary on `linux-x64` and `win-x64` every push to prove the toolchain stays clean.

Three Phase B integration libraries — Newtonsoft.Json, MongoDB.Driver, and Swashbuckle.AspNetCore — are not AOT-clean as of mid-2026. Their OrionKey emitters continue to work in non-AOT projects; if your project publishes AOT, prefer `System.Text.Json`, EF Core, and the BCL `TypeConverter` / `IParsable` pipelines. See [sample/Moongazing.OrionKey.AotSample](sample/Moongazing.OrionKey.AotSample) for a working end-to-end example.

Two AOT-specific patterns the sample demonstrates:

- **`System.Text.Json` with a source-generated context.** Source generators don't see each other's emitted attributes, so the `[JsonConverter]` attribute OrionKey emits is invisible to the `System.Text.Json` source generator. Register the generated converters into a `JsonSerializerOptions.Converters` collection at startup, then construct your `JsonSerializerContext` with those options (`new MyContext(options)`) and serialize via the context's per-type properties.
- **`IParsable<T>`.** OrionKey emits `Parse(string, IFormatProvider?)` as an explicit interface implementation. Call it through a generic constraint `where T : IParsable<T>` and invoke `T.Parse(text, null)` — the C# 11 static-abstract-interface-member syntax — rather than `MyId.Parse(...)` (which is not a public static method on the struct).

**Dapper note:** The OrionKey-generated `<Id>DapperTypeHandler` is AOT-compatible, but the Dapper assembly itself (2.1.35) produces aggregate `IL2104`/`IL3053` warnings during AOT publish because the Dapper team has not yet annotated it as trim-safe. AOT consumers can either suppress these per-assembly warnings (at their own risk) or wait for an upstream Dapper release that ships trim/AOT annotations. The AOT sample in this repo deliberately omits Dapper for that reason.

## Snowflake worker IDs

Snowflake ids embed a per-process **worker ID** (10 bits, 0-1023) to stay unique across
instances. Configure it explicitly with
`OrionKey.Configure(o => o.SnowflakeWorkerId = N)` or the `ORIONKEY_WORKER_ID` environment
variable; otherwise OrionKey derives one from the machine name and writes a one-time
warning. In any multi-instance deployment you should pin the worker ID explicitly. See
[docs/snowflake-worker-ids.md](docs/snowflake-worker-ids.md) for details.

## Benchmarks

See [benchmarks.md](benchmarks.md) for the full run, environment, and per-strategy interpretation. Headline numbers from the last measured run on an Intel Core i7-7820HQ (Kaby Lake), .NET 10.0.5, BenchmarkDotNet 0.14.0:

- `Guid.NewGuid()` baseline: 70 ns, 0 B allocated.
- Snowflake (sortable long): 241 ns, 0 B.
- ULID (sortable string): 102 ns, 80 B.
- UUIDv7 (sortable Guid): 122 ns, 0 B.

Reproduce with `dotnet run -c Release --project bench/Moongazing.OrionKey.Benchmarks`.

## Testing

The `OrionKey.Testing` package makes generated ids predictable in tests. A
`DeterministicIdScope` overrides the active generators for its lifetime, and the bundled
sequential generators hand out ascending, repeatable ids so assertions do not depend on
random or time-based values. Wrap the code under test in a scope and the ids it mints
become deterministic.

## Roadmap

OrionKey ships in phased minor releases on the way to 1.0:

- **`0.2.0` — New ID strategies** *(Done, 2026-05-22)* — `Cuid2`, `Ksuid`, `ObjectId`, `SequentialGuid`, plus byte-order GUID and ordinal-string `CompareTo` fixes.
- **`0.3.0` — Integration emitters** *(Done, 2026-05-23)* — conditional emitters for Dapper, Newtonsoft.Json, MongoDB, and Swashbuckle/OpenAPI, plus per-library aggregate registrars.
- **`0.3.1` — Logo refresh** *(Done, 2026-05-23)* — new minimalist family-style key logo in Moongazing indigo; no code changes.
- **`0.4.0` — Native AOT & trimming** *(Done)* — full `PublishAot`/`PublishTrimmed` compatibility with a verified AOT sample app and CI publish job.
- **`0.5.0` — Analyzer, code-fix, stabilization** *(Done, 2026-06-01)* — new diagnostics (`ORIONKEY006`–`008`), code-fix providers, source-generator performance pass.
- **`0.6.0` — Source-gen JSON path & `MonotonicHex`** *(Done, 2026-06-19)* — reflection-free `System.Text.Json` registrar (`OrionKeyJsonRegistrar.AddTo`) for the AOT source-generation path, and the sortable, monotonic `MonotonicHex` string strategy.
- **`0.7.0` — EF Core value-converter ergonomics** *(Done, 2026-07-20)* — a new `OrionKey.EntityFrameworkCore` sub-package whose `UseOrionKeyConversions()` wires the generated value converters across a whole model in one call.
- **Composite IDs & extra emitters** *(Planned)* — multi-value tuple IDs, `IUtf8SpanFormattable`/`IUtf8SpanParsable`, `Tsid`/`Xid` strategies.
- **`1.0.0` — Stable API** *(Planned, Q2 2027)* — public-type and emitter-contract freeze, LTS window, `net8.0` drop decision.

Full roadmap with *Considered* and *Out of scope* sections lives in
[docs/ROADMAP.md](docs/ROADMAP.md). If something on the list matters to you, open an issue
with the `roadmap` label.

## More from the Orion family

OrionKey is one of a set of standalone .NET libraries:

- [OrionGuard](https://github.com/tunahanaliozturk/OrionGuard) - guard clauses, validation, DDD primitives for .NET.
- [OrionAudit](https://github.com/tunahanaliozturk/OrionAudit) - automatic EF Core change-audit trail.
- [OrionPatch](https://github.com/tunahanaliozturk/OrionPatch) - transactional outbox primitive (enqueue inside EF Core SaveChanges, dispatch at-least-once through a pluggable sink).
- [OrionVault](https://github.com/tunahanaliozturk/OrionVault) - column-level transparent data encryption at rest for EF Core.

### See it in a real app

[Moongazing.OrionShowcase](https://github.com/tunahanaliozturk/OrionShowcase) is a production-shaped banking sample integrating all six Orion packages end-to-end. The OrionKey static facade generates Snowflake IDs for command audit rows; an EF-backed IdempotencyStore bridges the MediatR IdempotencyBehavior to OrionKey-derived identifiers. Concrete usage:

- [src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs](https://github.com/tunahanaliozturk/OrionShowcase/blob/main/src/Moongazing.OrionShowcase.Infrastructure/Audit/EfAuditWriter.cs)
- [src/Moongazing.OrionShowcase.Infrastructure/Idempotency/OrionKeyIdempotencyStore.cs](https://github.com/tunahanaliozturk/OrionShowcase/blob/main/src/Moongazing.OrionShowcase.Infrastructure/Idempotency/OrionKeyIdempotencyStore.cs)

## Contributing

Issues and pull requests welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md) before opening one.

## License

OrionKey is released under the MIT License. See [LICENSE.txt](LICENSE.txt).
