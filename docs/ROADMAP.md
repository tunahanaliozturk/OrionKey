# OrionKey Roadmap

OrionKey is a source-generated strongly-typed ID library for .NET. The roadmap below is the
public view of where the library is going. Each phase ships as an independent minor release
with its own spec, plan, tests, and changelog entry; this document is the living index. It is
a planning artifact, not a contract — dates slip, priorities reshuffle. If an item here
matters to you, open a GitHub issue so we can weigh it against everything else.

Status legend: **Done** (shipped) · **Planned** (designed, target window committed) ·
**Considered** (interesting but unscheduled, needs a concrete use case) · **Out of scope**
(explicitly declined for the 1.x line).

---

## Phase A — `0.2.0` · New ID strategies

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

## Phase B — `0.3.0` · Integration emitters

**Status:** Done · **Shipped:** 2026-05-23 · [Changelog](../CHANGELOG.md#030---2026-05-23) · **Spec:**
[2026-05-23 design](superpowers/specs/2026-05-23-orionkey-0.3.0-integration-emitters-design.md)

OrionKey 0.2 already covers `System.Text.Json`, EF Core, ASP.NET Core model binding, and the
BCL `TypeConverter` pipeline. Phase B extends that coverage to four widely-used libraries,
each behind a conditional emitter that fires only when the consumer references the target
package — same pattern the EF Core converter uses today.

- **Dapper** — emit `SqlMapper.TypeHandler<TId>` so OrionKey IDs work as parameters and
  result columns in raw Dapper queries.
- **Newtonsoft.Json** — emit a `JsonConverter<TId>` for projects still on `Newtonsoft.Json`.
- **MongoDB driver** — emit `SerializerBase<TId>` so IDs persist in BSON documents.
- **OpenAPI / Swashbuckle** — emit `ISchemaFilter` entries so generated OpenAPI documents
  show the ID's underlying primitive type, not an opaque struct.

A small registration helper (`services.AddOrionKey()` or per-library `Register()` methods)
will reduce boilerplate. Out of scope for this phase: new ID strategies, AOT work,
analyzer/code-fix changes.

---

## Logo refresh — `0.3.1`

**Status:** Done · **Shipped:** 2026-05-23

New minimalist family-style key logo in Moongazing indigo (`#312E81`), aligned with the
sibling OrionGuard, OrionAudit, and OrionLock packages. No code changes.

---

## Phase C — `0.4.0` · Native AOT & trimming

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
public-API surface — purely making existing APIs AOT-clean.

---

## Phase D — `0.5.0` · Analyzer, first slice *(shipped 2026-06-01)*

**Status:** Done

Two new diagnostics shipped:

- **ORIONKEY006** (Warning) - entity ID used as an EF Core key without an explicit `HasConversion`.
- **ORIONKEY007** (Info) - `[OrionId]` struct declared but never referenced.

Both run under `WellKnownDiagnosticTags.CompilationEnd` so they aggregate across the whole compilation. Severities respect `.editorconfig` tuning per standard Roslyn conventions.

### Deferred to follow-up patches

The remaining v0.5.0 milestone items did not ship in this release. New targets:

- **ORIONKEY008** (suggestion to promote bare `Guid`/`long` properties named `Id`/`*Id` to typed ids) -> v0.5.1.
- **ORIONKEY003 / ORIONKEY005 code-fix providers** -> v0.5.2.
- **Source-generator performance pass** (incremental-generator caching audit + large-solution benchmark) -> v0.5.3.

Public-API kept stable; any remaining edge-case fixes fold into the 0.5.x patches so 1.0 stays bug-fix-only afterwards.

---

## Phase E — `0.6.0` · Composite IDs & extra emitters

**Status:** Planned · **Target:** Q1 2027

The last 0.x release before the API freeze. Pulls the two highest-signal items out of
*Beyond 1.0* and ships them so they get a real production cycle before 1.0 locks the surface.

- **Multi-value / composite IDs.** `[StronglyTypedId(typeof((Guid TenantId, long LocalId)))]`
  for domains that genuinely need a compound primary key. Equality, parsing, JSON, and EF Core
  value conversion all extend cleanly to the tuple shape.
- **`IUtf8SpanFormattable` / `IUtf8SpanParsable` emitters** for zero-allocation
  serialization in hot paths.
- **`Tsid` and `Xid` strategies** added to the existing strategy matrix. Both are widely-used
  k-sortable formats and slot in next to `Ksuid` / `Cuid2` without architectural changes.

---

## `1.0.0` · Stable API

**Status:** Planned · **Target:** Q2 2027

The 1.0 release is a commitment: public types and emitter contracts freeze inside the 1.x
line. Anything obsolete by then is removed; everything that remains is stable.

- **API stability.** `[StronglyTypedId]`, `IStronglyTypedId<TValue>`, every emitted
  member shape, and every integration emitter (EF Core, System.Text.Json, Dapper,
  Newtonsoft, MongoDB, OpenAPI) freeze. Additions only.
- **Diagnostic stability.** `ORIONKEY001`–`ORIONKEY008` ids are permanent; any new
  diagnostics get fresh ids.
- **Documentation pass.** Every emitted member documented with a runnable example; migration
  guide from any breaking change introduced in 0.x; strategy-selection cookbook
  ("`Guid` vs `GuidV7` vs `SequentialGuid` vs `Ulid` vs `Snowflake`").
- **net8.0 drop decision.** Decide and publish whether 1.x ships TFM `net8.0` or starts at
  `net9.0`. This is the last chance to cut net8 before SemVer locks it in.
- **LTS window.** Security and correctness fixes backported to 1.x for 18 months after 2.0
  ships.

---

## Considered (no commitment)

Ideas under discussion that need a concrete use case before they move to *Planned*. Raise an
issue if any of them matter to you.

- **gRPC / protobuf integration emitter.** Same conditional-emitter pattern as Dapper /
  MongoDB; would round out the integration matrix.
- **`OrionKey.SourceLink` companion** that lets debuggers step into generated ID code
  without copying it into the solution.
- **Built-in `OrionKey.AspNetCore.OpenApi` package** for the newer
  `Microsoft.AspNetCore.OpenApi` (net9+) on top of the existing Swashbuckle emitter.
- **Public `Roslyn` API surface** so consumers can write their own analyzers/codefixes that
  understand OrionKey ids at compile time.

If any of the above maps to a real workload you are on right now, open an issue with the
`roadmap` label and a short description — that is how items move from *considered* to
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

| Release | Target             | Theme                                              |
| ------- | ------------------ | -------------------------------------------------- |
| v0.2.0  | shipped 2026-05-22 | New ID strategies                                  |
| v0.3.0  | shipped 2026-05-23 | Integration emitters (Dapper, Newtonsoft, Mongo, OpenAPI) |
| v0.3.1  | shipped 2026-05-23 | Logo refresh                                       |
| v0.4.0  | shipped 2026-05-24 | Native AOT & trimming                              |
| v0.5.0  | Q4 2026            | Analyzer, code-fix, stabilization                  |
| v0.6.0  | Q1 2027            | Composite IDs & extra emitters                     |
| v1.0.0  | Q2 2027            | API freeze, LTS window                             |

Patch releases ship as needed for bugs and security. Minor releases cluster features around
the themes above and never break documented public APIs without a deprecation cycle. Dates
are targets, not commitments. If a milestone slips by more than four weeks, the delay shows
up here.

---

## Contributing & feedback

Issues, design discussions, and PRs are welcome on
[GitHub](https://github.com/tunahanaliozturk/OrionKey). If a phase's scope does not match
your needs, the spec files linked above are the right place to comment — they capture the
design decisions before implementation begins.
