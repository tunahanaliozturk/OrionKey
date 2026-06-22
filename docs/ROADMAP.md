# OrionKey Roadmap

OrionKey is a source-generated strongly-typed ID library for .NET. The roadmap below is the
public view of where the library is going. Each phase ships as an independent minor release
with its own spec, plan, tests, and changelog entry; this document is the living index. It is
a planning artifact, not a contract - dates slip, priorities reshuffle. If an item here
matters to you, open a GitHub issue so we can weigh it against everything else.

**Current release: `0.6.2`** (2026-06-20). Latest work: a reflection-free, AOT/trimming-safe
System.Text.Json source-generation path (`OrionKeyJsonRegistrar.AddTo`), the sortable
`MonotonicHex` string strategy (both `0.6.0`), and a single-allocation lowercase-hex
formatting pass on the hex-rendered strategies (`0.6.2`). Next minor is `0.7.0` (composite
ids and the remaining strategy/emitter work); `1.0.0` freezes the public API.

Status legend: **Done** (shipped) · **Planned** (designed, target window committed) ·
**Considered** (interesting but unscheduled, needs a concrete use case) · **Out of scope**
(explicitly declined for the 1.x line).

---

## Phase A - `0.2.0` · New ID strategies

**Status:** Done · **Shipped:** 2026-05-22 · [Changelog](../CHANGELOG.md#020---2026-05-22)

- Four new generation strategies: `Cuid2` (24-char base36), `Ksuid` (27-char sortable base62),
  `ObjectId` (24-char hex MongoDB-style), `SequentialGuid` (SQL Server-ordered index-friendly GUIDs).
- New runtime factories and `OrionKey` facade members; deterministic counterparts in
  `OrionKey.Testing`.
- Two latent ordering-bug fixes folded in: GUID-backed sortable `CompareTo` now compares in
  byte order (was field-by-field, did not preserve creation order); string-backed sortable
  `CompareTo` now uses ordinal comparison (was culture-sensitive).
- `OrionGuidComparer` runtime helper for byte-order GUID comparison.

---

## Phase B - `0.3.0` · Integration emitters

**Status:** Done · **Shipped:** 2026-05-23 · [Changelog](../CHANGELOG.md#030---2026-05-23) · **Spec:**
[2026-05-23 design](superpowers/specs/2026-05-23-orionkey-0.3.0-integration-emitters-design.md)

OrionKey 0.2 already covers `System.Text.Json`, EF Core, ASP.NET Core model binding, and the
BCL `TypeConverter` pipeline. Phase B extends that coverage to four widely-used libraries,
each behind a conditional emitter that fires only when the consumer references the target
package, the same pattern the EF Core converter uses today.

- **Dapper** - emit `SqlMapper.TypeHandler<TId>` so OrionKey IDs work as parameters and
  result columns in raw Dapper queries.
- **Newtonsoft.Json** - emit a `JsonConverter<TId>` for projects still on `Newtonsoft.Json`.
- **MongoDB driver** - emit `SerializerBase<TId>` so IDs persist in BSON documents.
- **OpenAPI / Swashbuckle** - emit `ISchemaFilter` entries so generated OpenAPI documents
  show the ID's underlying primitive type, not an opaque struct.

A small registration helper (`services.AddOrionKey()` or per-library `Register()` methods)
will reduce boilerplate. Out of scope for this phase: new ID strategies, AOT work,
analyzer/code-fix changes.

---

## Logo refresh - `0.3.1`

**Status:** Done · **Shipped:** 2026-05-23

New minimalist family-style key logo in Moongazing indigo (`#312E81`), aligned with the
sibling OrionGuard, OrionAudit, and OrionLock packages. No code changes.

---

## Phase C - `0.4.0` · Native AOT & trimming

**Status:** Done · **Shipped:** 2026-05-24 · [Changelog](../CHANGELOG.md#040---2026-05-24) · **Spec:**
[2026-05-23 design](superpowers/specs/2026-05-23-orionkey-0.4.0-aot-trimming-design.md)

Make OrionKey safe and friction-free in `PublishAot` and `PublishTrimmed` deployments.

- Mark every runtime project with `<IsAotCompatible>true</IsAotCompatible>` and resolve
  every trim/AOT warning at the source.
- Audit reflection use across `TypeConverter` discovery, EF Core convention scanning, and
  ASP.NET Core model binding; add precise `[DynamicallyAccessedMembers]` and
  `[UnconditionalSuppressMessage]` annotations where the reflection is provably safe.
- Add a new `sample/Moongazing.OrionKey.AotSample` that publishes with `PublishAot=true` and
  exercises every ID strategy plus every Phase B emitter as a smoke test.
- Wire an AOT-publish job into CI so regressions surface before they ship.

Out of scope: new ID strategies, new emitters, analyzer changes. Goal is zero new
public-API surface, purely making existing APIs AOT-clean.

---

## Phase D - `0.5.0` · Analyzer, first slice *(shipped 2026-06-01)*

**Status:** Done

Two new diagnostics shipped:

- **ORIONKEY006** (Warning) - entity ID used as an EF Core key without an explicit `HasConversion`.
- **ORIONKEY007** (Info) - `[OrionId]` struct declared but never referenced.

Both run under `WellKnownDiagnosticTags.CompilationEnd` so they aggregate across the whole compilation. Severities respect `.editorconfig` tuning per standard Roslyn conventions.

### Delivered across the rest of the 0.5.x line (through `0.5.31`)

The items deferred from the first slice all shipped over the `0.5.x` patches, alongside more
that surfaced along the way. See the [changelog](../CHANGELOG.md) for the per-version detail.

- **Analyzers** `ORIONKEY006`-`ORIONKEY008` plus `ORIONKEY010`-`ORIONKEY012` (bare-id property,
  method-parameter, and method-return promotion; redundant id-property naming).
- **Code-fix providers** in the new `Moongazing.OrionKey.CodeFixes` assembly for ORIONKEY003,
  ORIONKEY004, ORIONKEY005, ORIONKEY007, ORIONKEY008, ORIONKEY010, ORIONKEY011, and
  ORIONKEY012, all with FixAll support.
- **Source-generator performance pass**: a value-equatable `ParsedOrionId` / `DiagnosticInfo`
  pipeline so the incremental cache is actually reused across keystrokes, with
  `IncrementalCacheTests` guarding it.
- **Wider emitted surface**: public throwing `Parse`, `ReadOnlySpan<char>` / UTF-8 `TryParse`,
  `IFormattable` / `ISpanFormattable` / `IUtf8SpanFormattable` / `IUtf8SpanParsable`,
  `IComparable` for every id type, `WrapAll` / `UnwrapAll` / `CreateMany` / `ParseOrDefault` /
  `IsEmpty`, `HasOrionKeyConversion()`, and `OrionKeyTypeRegistry` for AOT-friendly cross-type
  dispatch.

Public API stayed additive throughout, so 1.0 remains a freeze-and-document step rather than a
breaking one.

---

## Phase E - `0.6.0` · System.Text.Json source-gen path & `MonotonicHex`

**Status:** Done · **Shipped:** 2026-06-19 · [Changelog](../CHANGELOG.md#060---2026-06-19)

Shipped the two highest-signal items that were ready, ahead of the composite-id work that
moved to `0.7.0`.

- **System.Text.Json source-generation path.** `OrionKeyJsonConverterFactory`, a
  reflection-free `JsonConverterFactory` that resolves every `[OrionId]` type through a
  compile-time `typeToConvert` switch (no `MakeGenericType`, NativeAOT- and trimming-safe),
  plus `OrionKeyJsonRegistrar.AddTo(JsonSerializerOptions)` to register every generated
  converter in one call. Pairs with a `JsonSerializerContext` constructed over those options
  so ids round-trip through the source-generation metadata path while still emitting the bare
  scalar shape. The bundled AOT sample now wires its context this way.
- **`MonotonicHex` strategy** (`[OrionId<string, MonotonicHex>]`): a 48-bit big-endian
  millisecond timestamp followed by 80 bits of randomness, rendered as 32 lowercase hex
  characters. Ordinal string order equals chronological order; ids minted in the same
  millisecond are strictly increasing within a process (the randomness block increments rather
  than re-draws); clock regressions hold the previous timestamp so the sequence never
  regresses. Adds `MonotonicHexFactory.NewMonotonicHex()` and the matching `OrionKey` facade
  member, and participates in the JSON / parse / EF / Dapper / Mongo / OpenAPI surfaces like
  the other string strategies.

Note: the `IUtf8SpanFormattable` / `IUtf8SpanParsable` emitters originally grouped under this
phase shipped earlier, in `0.5.27` / `0.5.28`.

---

## Patch line - `0.6.1` · `0.6.2`

**Status:** Done · **Shipped:** 2026-06-20 · [Changelog](../CHANGELOG.md#062---2026-06-20)

- `0.6.1`: the diagnostics meter version is derived from the assembly informational version
  rather than a hardcoded literal, so it tracks the package version automatically.
- `0.6.2`: `MonotonicHexFactory.NewMonotonicHex()` and `ObjectIdFactory.NewObjectId()` render
  their lowercase-hex output in a single allocation via a shared `HexFormat.ToLowerHex` helper,
  replacing `Convert.ToHexString(...).ToLowerInvariant()` which allocated an intermediate
  uppercase string. Output is byte-identical; a throwaway BenchmarkDotNet run measured roughly
  26% faster for the 16-byte MonotonicHex path and 45% faster for the 12-byte ObjectId path,
  each halving per-id allocation.

---

## Phase F - `0.7.0` · Composite IDs & extra strategies

**Status:** Planned · **Target:** Q4 2026

The last 0.x feature release before the API freeze. Carries the composite-id work and the
remaining strategies that did not make `0.6.0`, so each gets a real production cycle before
`1.0.0` locks the surface.

- **Multi-value / composite IDs.** `[OrionId(typeof((Guid TenantId, long LocalId)))]` for
  domains that genuinely need a compound key. Equality, ordering, parsing, `System.Text.Json`,
  and EF Core value conversion all extend to the tuple shape; the EF Core path emits the
  per-component column split rather than a single opaque blob.
- **`Tsid` and `Xid` strategies** added to the existing strategy matrix. Both are widely-used
  k-sortable formats and slot in next to `Ksuid` / `Cuid2` / `MonotonicHex` without
  architectural changes.
- **EF Core value-converter ergonomics.** A model-wide
  `modelBuilder.UseOrionKeyConversions()` (or convention) that registers every `[OrionId]`
  converter in one call, so consumers stop wiring `HasOrionKeyConversion()` property by
  property. Complements the per-property helper shipped in `0.5.10`.
- **Minimal-API route-binding helper.** A small `AddOrionKeyRouteBinding()` /
  `IParsable`-backed binder so `[OrionId]` types bind from route and query values with a clear
  400 on malformed input, on top of the `TypeConverter` path that already works today.

Composite ids are the large item here; if they slip, the strategy and ergonomics items ship on
their own and composites move to a `0.7.x` follow-up rather than holding the release.

---

## `1.0.0` · Stable API

**Status:** Planned · **Target:** Q2 2027

The 1.0 release is a commitment: public types and emitter contracts freeze inside the 1.x
line. Anything obsolete by then is removed; everything that remains is stable.

- **API stability.** The `[OrionId<TValue>]` / `[OrionId<TValue, TStrategy>]` attributes,
  every emitted member shape, and every integration emitter (EF Core, System.Text.Json
  including the source-gen registrar, Dapper, Newtonsoft, MongoDB, OpenAPI) freeze. Additions
  only.
- **Diagnostic stability.** The `ORIONKEY0xx` ids shipped through 0.x are permanent; any new
  diagnostic gets a fresh id and is never reused.
- **AOT / trimming guarantee.** The `<IsAotCompatible>true</IsAotCompatible>` story and the
  CI AOT-publish gate become a 1.x contract: the `System.Text.Json`, EF Core, and BCL
  `TypeConverter` / `IParsable` paths stay reflection-free and AOT-clean. The non-AOT-clean
  integration libraries (Newtonsoft, MongoDB, Swashbuckle) keep working but remain documented
  as non-AOT.
- **Documentation pass.** Every emitted member documented with a runnable example; migration
  guide from any breaking change introduced in 0.x; strategy-selection cookbook
  ("`Guid` vs `GuidV7` vs `SequentialGuid` vs `Ulid` vs `Snowflake` vs `MonotonicHex`").
- **net8.0 drop decision.** Decide and publish whether 1.x keeps TFM `net8.0` (alongside the
  current `net9.0` / `net10.0`) or starts at `net9.0`. This is the last chance to cut net8
  before SemVer locks it in.
- **LTS window.** Security and correctness fixes backported to 1.x for 18 months after 2.0
  ships.

---

## Considered (no commitment)

Ideas under discussion that need a concrete use case before they move to *Planned*. Raise an
issue if any of them matter to you.

- **MessagePack / protobuf integration emitters.** Same conditional-emitter pattern as Dapper
  / MongoDB: a `MessagePack` formatter and a protobuf surrogate so ids round-trip as their
  underlying primitive in binary contracts and gRPC payloads. Would round out the integration
  matrix beyond the text/JSON formats shipped today.
- **`OrionKey.SourceLink` companion** that lets debuggers step into generated ID code
  without copying it into the solution.
- **Built-in `OrionKey.AspNetCore.OpenApi` package** for the newer
  `Microsoft.AspNetCore.OpenApi` (net9+) on top of the existing Swashbuckle emitter.
- **Wider analyzer coverage.** Diagnostics for the remaining mix-up sites the current rules do
  not reach (record positional parameters, collection-typed id properties) and a code-fix for
  any new rule, continuing the `ORIONKEY0xx` line shipped through 0.5.x.
- **Public `Roslyn` API surface** so consumers can write their own analyzers/codefixes that
  understand OrionKey ids at compile time.

If any of the above maps to a real workload you are on right now, open an issue with the
`roadmap` label and a short description - that is how items move from *considered* to
*planned*.

---

## Out of scope for the 1.x line

- **A runtime ID-generation service.** OrionKey emits the type and its plumbing; the
  generator strategy runs in-process. Distributed snowflake coordination, ID-allocation
  reservation services, and similar are deliberately out of scope.
- **Cross-language ID interop.** OrionKey is a .NET source generator. Sharing ids with a
  Java/Go service is a serialization concern, not an OrionKey concern.
- **Database-native ID generation features.** PostgreSQL `gen_random_uuid()`, SQL Server
  `NEWSEQUENTIALID()`, etc. live in your migration / DDL, not in OrionKey.

---

## Release cadence

| Release        | Target             | Theme                                                       |
| -------------- | ------------------ | ----------------------------------------------------------- |
| v0.2.0         | shipped 2026-05-22 | New ID strategies                                           |
| v0.3.0         | shipped 2026-05-23 | Integration emitters (Dapper, Newtonsoft, Mongo, OpenAPI)   |
| v0.3.1         | shipped 2026-05-23 | Logo refresh                                                |
| v0.4.0         | shipped 2026-05-24 | Native AOT & trimming                                       |
| v0.5.0-v0.5.31 | shipped 2026-06-17 | Analyzers, code-fixes, generator perf, parse/format surface |
| v0.6.0         | shipped 2026-06-19 | System.Text.Json source-gen path & MonotonicHex             |
| v0.6.2         | shipped 2026-06-20 | Single-allocation lowercase-hex formatting                  |
| v0.7.0         | Q4 2026            | Composite IDs, Tsid/Xid, EF Core & route-binding ergonomics |
| v1.0.0         | Q2 2027            | API freeze, AOT guarantee, LTS window                       |

Patch releases ship as needed for bugs and security. Minor releases cluster features around
the themes above and never break documented public APIs without a deprecation cycle. Dates
are targets, not commitments. If a milestone slips by more than four weeks, the delay shows
up here.

---

## Contributing & feedback

Issues, design discussions, and PRs are welcome on
[GitHub](https://github.com/tunahanaliozturk/OrionKey). If a phase's scope does not match
your needs, the spec files linked above are the right place to comment - they capture the
design decisions before implementation begins.
