# OrionKey v0.1.0 — Source-generated strongly-typed IDs

**Date:** 2026-05-20
**Status:** Approved (design); pending implementation plan
**Solution:** `Moongazing.OrionKey`
**Primary package:** `OrionKey`
**Repository:** new standalone repo (`Desktop/OrionKey/`), sibling to `OrionGuard` and `OrionAudit`

## 1. Goal

Ship a standalone .NET library that turns a single attribute into a fully-featured strongly-typed ID:

```csharp
[OrionId<Guid>]              public readonly partial struct OrderId;
[OrionId<long, Snowflake>]   public readonly partial struct UserId;
[OrionId<string, Ulid>]      public readonly partial struct TenantId;
[OrionId<string, NanoId>]    public readonly partial struct SessionId;
```

Everything else — equality, comparison, `New()` factory, EF Core `ValueConverter`, `System.Text.Json` converter, `TypeConverter`, `IParsable<T>` / `ISpanParsable<T>`, ASP.NET Core minimal-API binding — is emitted at compile time by a bundled Roslyn incremental source generator.

OrionKey graduates the strongly-typed-id feature out of `OrionGuard.DDD` into a single-purpose package that any project can add without inheriting a guard/validation library.

## 2. Position in the Orion family

OrionKey follows the Orion family quality bar defined in the family roadmap:

- **Standalone** — independent repo, independent NuGet package, no `<ProjectReference>` or `<PackageReference>` on any other Orion library, no third-party runtime dependency.
- **Focused** — strongly-typed IDs only.
- **Modern .NET** — multi-target `net8.0;net9.0;net10.0`, `TreatWarningsAsErrors=true`, AOT-aware.
- **Production-grade** — comprehensive tests, benchmarks, documented failure modes.

The shared "Orion" brand is a quality bar, not a code dependency.

### Non-goals (explicitly out of scope for v0.1.0)

- No dependency on `Cysharp.Ulid` or any third-party ID library — OrionKey implements ULID/NanoId/Snowflake itself.
- No distributed Snowflake coordination service (worker IDs are configured/derived, not leased from a registry).
- No `OrionGuard` modification — soft-deprecating `OrionGuard`'s `[StronglyTypedId]` is a *downstream* OrionGuard change tracked in §13, not implemented here.
- No DI-resolved per-call ID generator — `New()` is a static factory; see §6.

## 3. Solution & package layout

```text
Moongazing.OrionKey.sln
├── src/
│   ├── Moongazing.OrionKey            -> NuGet: OrionKey
│   ├── Moongazing.OrionKey.Generators -> Roslyn generator (not independently packed)
│   └── Moongazing.OrionKey.Testing    -> NuGet: OrionKey.Testing
├── tests/
│   ├── Moongazing.OrionKey.Tests
│   ├── Moongazing.OrionKey.Generators.Tests
│   ├── Moongazing.OrionKey.IntegrationTests
│   └── Moongazing.OrionKey.Testing.Tests
├── bench/
│   └── Moongazing.OrionKey.Benchmarks
├── sample/
│   └── Moongazing.OrionKey.Sample
├── Directory.Build.props
├── README.md
├── CHANGELOG.md
└── docs/
```

### 3.1 The `OrionKey` package is a single NuGet containing runtime + analyzer

`OrionKey` ships:

- `lib/net8.0|net9.0|net10.0/Moongazing.OrionKey.dll` — the runtime: the `[OrionId]` attributes, strategy marker types, the ID generators, `OrionKey.Configure`.
- `analyzers/dotnet/cs/Moongazing.OrionKey.Generators.dll` — the source generator + analyzer.

`Moongazing.OrionKey.Generators` exists in the solution as a development project but is **not** published as its own package; its build output is packed into the `OrionKey` package as an analyzer. A consumer runs `dotnet add package OrionKey` and gets both halves. This matches the "one attribute, everything emitted" promise.

### 3.2 Why a runtime package is mandatory (not pure codegen)

`OrionGuard`'s `[StronglyTypedId]` emits self-contained structs with no runtime dependency. OrionKey **cannot** be pure codegen because the Snowflake strategy requires process-wide shared mutable state:

- A Snowflake ID is `41-bit timestamp | 10-bit worker | 12-bit sequence`. The sequence counter must be shared across every `UserId.New()` call in the process — otherwise two structs each keep their own counter and produce colliding IDs within the same millisecond.

Therefore the generated `New()` delegates to a shared runtime generator. ULID (monotonic-within-millisecond) and NanoId (a pooled crypto-RNG buffer) also benefit from a shared runtime component. Consequently **all** strategies route through the `OrionKey` runtime for consistency — see §7.

## 4. The `[OrionId]` attribute

Two generic attribute forms (C# 11 generic attributes; valid on `net8.0+`):

```csharp
namespace Moongazing.OrionKey;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue> : Attribute;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue, TStrategy> : Attribute;
```

`TValue` is the storage primitive. `TStrategy` is a marker type selecting the generation algorithm.

### 4.1 Valid `TValue` / `TStrategy` combinations

| Declaration | Storage | `New()` behaviour | Sortable |
|---|---|---|---|
| `[OrionId<Guid>]` | `System.Guid` | `Guid.NewGuid()` | no |
| `[OrionId<Guid, GuidV7>]` | `System.Guid` | `Guid.CreateVersion7()` (net9+); polyfill on net8 | yes |
| `[OrionId<long, Snowflake>]` | `long` | Snowflake via shared generator | yes |
| `[OrionId<string, Ulid>]` | `string` | ULID, 26-char Crockford base32 | yes |
| `[OrionId<string, NanoId>]` | `string` | NanoId, 21-char URL-safe | no |
| `[OrionId<int>]` | `int` | none — externally assigned (DB identity) | n/a |
| `[OrionId<long>]` | `long` | none — externally assigned (DB identity) | n/a |

`int` and strategy-less `long` model database-identity columns: the struct wraps the primitive and gets all converters, but no `New()` factory (the database assigns the value). Every other declared form gets a `New()`.

### 4.2 Strategy marker types

```csharp
namespace Moongazing.OrionKey;

public readonly struct Snowflake;
public readonly struct Ulid;
public readonly struct NanoId;
public readonly struct GuidV7;
```

Empty marker structs used only as the `TStrategy` type argument. They carry no members.

### 4.3 Diagnostics (analyzer)

| ID | Severity | Condition |
|---|---|---|
| `ORIONKEY001` | Error | `[OrionId]` target is not a `readonly partial struct` |
| `ORIONKEY002` | Error | unsupported `TValue` (not Guid/int/long/string) |
| `ORIONKEY003` | Error | `TValue` is `string` but no `TStrategy` was supplied |
| `ORIONKEY004` | Error | `TStrategy` is incompatible with `TValue` (e.g. `[OrionId<Guid, Snowflake>]`, `[OrionId<long, Ulid>]`) |
| `ORIONKEY005` | Warning | the struct declares a member the generator also emits (name collision) |

Strategy-less declarations are disambiguated by `TValue`:

- `[OrionId<Guid>]` — valid; `Guid` implies its own generation (`Guid.NewGuid()`).
- `[OrionId<int>]` / `[OrionId<long>]` — valid; treated as **externally-assigned** IDs (database identity columns). The struct gets all converters but no `New()` factory.
- `[OrionId<string>]` — **`ORIONKEY003` error**. A bare `string` ID has neither a generation rule nor database-identity semantics, so it is meaningless; the author must supply `Ulid` or `NanoId`.

Absence of a strategy is itself a signal, so there is no ambiguous case.

## 5. Emitted companions

For each decorated struct the generator emits these partial sources (one logical file each, all under the consumer's namespace):

1. **Core partial body** — `TValue Value` property, primary constructor `Id(TValue value)`, `static Id Empty`, `IEquatable<Id>`, `operator ==` / `!=`, `GetHashCode`, `ToString`, and `static Id New()` when the strategy generates.
2. **`IComparable<Id>` + `operator <,<=,>,>=`** — emitted only for sortable strategies (Snowflake, Ulid, GuidV7). Sort order equals creation order.
3. **EF Core `ValueConverter<Id, TValue>`** — emitted **only** when the consumer's project references `Microsoft.EntityFrameworkCore` (conditional emission, same discipline as OrionGuard v6.2). Namespace `Microsoft.EntityFrameworkCore.Storage.ValueConversion`.
4. **`System.Text.Json.Serialization.JsonConverter<Id>`** — reads/writes the underlying primitive.
5. **`System.ComponentModel.TypeConverter`** — ASP.NET Core route/query/form model binding.
6. **`IParsable<Id>` / `ISpanParsable<Id>`** — ASP.NET Core minimal-API route/query binding without extra registration.

Companions 4–6 emit unconditionally. Companion 3 is conditional. Companion 2 is strategy-dependent.

## 6. Snowflake configuration

The generated `UserId.New()` is a `static` factory. A `static` method cannot resolve a service from DI, and threading an `ISnowflakeIdGenerator` parameter through every call site would defeat the ergonomic one-liner the library exists to provide. OrionKey therefore uses a **process-wide configured generator with a safe auto-fallback**:

```csharp
// Explicit (recommended for multi-instance deployments) — once at startup:
OrionKey.Configure(o =>
{
    o.SnowflakeWorkerId = 5;                       // 0..1023
    o.SnowflakeEpoch    = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc); // optional
});
```

When `Configure` is not called, the worker ID is derived in this order:

1. Environment variable `ORIONKEY_WORKER_ID` (parsed as `0..1023`).
2. Otherwise, a stable hash of `Environment.MachineName`, masked to 10 bits.

When the worker ID is auto-derived, OrionKey writes a one-time warning through `System.Diagnostics` (`OrionKeyDiagnostics` trace source) so operators of multi-instance deployments know to pin it explicitly. Auto-derivation makes single-process and local-dev usage zero-config; the warning prevents silent ID collisions across pods.

`OrionKey.Configure` is idempotent-safe to call once; a second call throws `InvalidOperationException` (configuration is process-global and must not change after the first ID is minted). `OrionKey.Testing` provides a sanctioned reset path — see §9.

## 7. Runtime types (`Moongazing.OrionKey`)

All under namespace `Moongazing.OrionKey` unless noted.

- `OrionIdAttribute<TValue>`, `OrionIdAttribute<TValue, TStrategy>` — §4.
- `Snowflake`, `Ulid`, `NanoId`, `GuidV7` — strategy markers, §4.2.
- `SnowflakeIdGenerator` — Twitter-Snowflake algorithm: `41-bit ms-timestamp | 10-bit worker | 12-bit sequence`. Thread-safe via a lock-free CAS loop on a packed `long` state word. Configurable epoch. Throws `OrionKeyClockException` on backwards clock movement beyond a small tolerance.
- `UlidFactory` — ULID: `48-bit ms-timestamp + 80-bit randomness`, Crockford base32, monotonic within a millisecond (randomness incremented, not re-rolled, when two IDs share a timestamp).
- `NanoIdFactory` — NanoId: 21 characters from the 64-char URL-safe alphabet `A-Za-z0-9_-`, sourced from a pooled `RandomNumberGenerator`.
- `OrionKey` (static) — `Configure(Action<OrionKeyOptions>)`; internal accessors the generated code calls (`OrionKey.NextSnowflake()`, `OrionKey.NewUlid()`, `OrionKey.NewNanoId()`).
- `OrionKeyOptions` — `SnowflakeWorkerId`, `SnowflakeEpoch`.
- `OrionKeyClockException` — thrown on Snowflake clock regression.

The generated `New()` is a thin delegation, e.g. `public static UserId New() => new(OrionKey.NextSnowflake());`. The runtime owns every algorithm; the generator owns only the struct shape and the converter companions.

### 7.1 No third-party runtime dependency

ULID (~150 lines) and NanoId (~60 lines) are implemented in-package. OrionKey takes no `PackageReference` at runtime beyond the BCL. This honours the family "standalone" rule and keeps the dependency graph of a consuming app unchanged by adding OrionKey.

## 8. OpenTelemetry / diagnostics

ID generation is a hot path; unconditional metrics are unwanted overhead. OrionKey exposes:

- A `System.Diagnostics.Metrics.Meter` named `Moongazing.OrionKey` with one opt-in counter `orion.key.ids.generated` tagged by `strategy`. The counter is only wired when the consumer enables it via `OrionKey.Configure(o => o.EnableMetrics = true)`; default off.
- A `TraceSource`/`ILogger`-agnostic one-time warning channel for the Snowflake auto-worker-id case (§6), implemented over `System.Diagnostics`.

No `ActivitySource` — there is no span-worthy operation in ID minting.

## 9. `OrionKey.Testing` package

Tests need deterministic IDs. `OrionKey.Testing` provides:

- `DeterministicIdScope` — an `IDisposable` that swaps the process-wide generators for deterministic sequences for the duration of a test, and restores them on dispose. Wraps the sanctioned reset path that production `OrionKey.Configure` does not expose.
- `SequentialSnowflake`, `SequentialUlid`, `SequentialNanoId` — generators producing predictable, ascending values (`1, 2, 3, ...` projected into each format) so test assertions can hard-code expected IDs.

`OrionKey.Testing` references `OrionKey` and nothing else. It is framework-agnostic (no xUnit/NUnit dependency).

## 10. Versioning & repository

- Version starts at **`0.1.0`** — a new pre-1.0 library, consistent with OrionAudit's `0.3.0` pre-1.0 line.
- `Directory.Build.props` mirrors OrionAudit: `TargetFrameworks=net8.0;net9.0;net10.0`, `TreatWarningsAsErrors=true`, `Nullable=enable`, `LangVersion=latest`, `AnalysisLevel=latest-recommended`, `GenerateDocumentationFile` for packable projects.
- New standalone git repository at `Desktop/OrionKey/`, default branch `main`.
- `RepositoryUrl` placeholder `https://github.com/tunahanaliozturk/OrionKey` (consistent with the family).

## 11. Testing strategy

- **`Moongazing.OrionKey.Tests`** — runtime unit tests: `SnowflakeIdGenerator` (uniqueness under parallel load, monotonicity, clock-regression exception, worker-id bit packing), `UlidFactory` (lexicographic sort order, within-ms monotonicity, round-trip parse), `NanoIdFactory` (alphabet, length, distribution sanity), `OrionKey.Configure` (idempotency, double-call throws), worker-id auto-derivation.
- **`Moongazing.OrionKey.Generators.Tests`** — generator snapshot/verification tests: each `TValue`/`TStrategy` combination emits the expected companions; conditional EF Core converter appears only with the EF Core reference; every diagnostic `ORIONKEY001`–`005` fires on its trigger.
- **`Moongazing.OrionKey.IntegrationTests`** — end-to-end: a generated ID round-trips through `System.Text.Json`, through an EF Core SQLite model (`ValueConverter`), and through an ASP.NET Core minimal-API route (`IParsable`) and an MVC route (`TypeConverter`).
- **`Moongazing.OrionKey.Testing.Tests`** — `DeterministicIdScope` swaps and restores generators correctly; sequential generators produce the documented sequence.
- **`Moongazing.OrionKey.Benchmarks`** — `New()` throughput per strategy; comparison against raw `Guid.NewGuid()`.

Coverage bar: every public runtime type has happy-path and negative-path tests; every generator combination and every diagnostic is covered.

## 12. Documentation deliverables

- `README.md` — quick start, the attribute table, the family "More from the Orion family" section.
- `CHANGELOG.md` — `[0.1.0]` initial release.
- `docs/snowflake-worker-ids.md` — operating Snowflake across multiple instances (env var, explicit config, the auto-derivation warning).
- Per-package README packed into each NuGet (`OrionKey`, `OrionKey.Testing`).

## 13. Downstream: OrionGuard integration (not in this spec)

After OrionKey v0.1.0 ships, a **separate OrionGuard change** will:

- Mark `OrionGuard`'s `[StronglyTypedId<TValue>]` `[Obsolete]` (soft-deprecation, warning not error).
- Point its XML doc and the OrionGuard docs at OrionKey as the successor.
- Optionally ship a thin re-export so existing `[StronglyTypedId]` users compile unchanged during the deprecation window.

This is tracked here for context only. It is an OrionGuard repository change with its own spec/plan, not part of OrionKey's implementation plan.

## 14. Out-of-scope confirmations

- No third-party runtime dependency (`Cysharp.Ulid` etc.).
- No distributed worker-id lease service.
- No OrionGuard code change in this repository.
- No non-`readonly`-`struct` ID targets (classes, records).
- No custom user-supplied strategies in v0.1.0 (the four built-ins only); a pluggable `IIdStrategy` is a post-1.0 consideration.
