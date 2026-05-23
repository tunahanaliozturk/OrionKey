# OrionKey Roadmap

OrionKey is a source-generated strongly-typed ID library for .NET. The roadmap below is the
public view of where the library is going. Each phase ships as an independent minor release
with its own spec, plan, tests, and changelog entry; this document is the living index.

Status legend: **Done** (shipped) · **Planned** (designed, not yet implemented) · **Idea**
(post-1.0, not yet designed).

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

**Status:** Planned · **Spec:**
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

## Phase C — `0.4.0` · Native AOT & trimming

**Status:** Planned · **Spec:**
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

## Phase D — `0.5.0` · Analyzer, code-fix, and stabilization

**Status:** Planned · **Spec:**
[2026-05-23 design](superpowers/specs/2026-05-23-orionkey-0.5.0-analyzer-improvements-design.md)

Help users catch misuse before they ship it, and stabilize the public API for 1.0.

- New diagnostics:
  - **ORIONKEY006** — entity ID used as an EF Core key without an explicit `HasConversion`.
  - **ORIONKEY007** — `[OrionId]` struct declared but never referenced.
  - **ORIONKEY008** — bare `Guid`/`long` property named `Id`/`*Id` that could be promoted
    to a strongly-typed ID (suggestion).
- Code-fix providers:
  - **ORIONKEY003 fix** — "string requires an explicit strategy" → quick-fix that picks one
    of `Ulid` / `NanoId` / `Cuid2` / `Ksuid` / `ObjectId`.
  - **ORIONKEY005 fix** — member-collision diagnostic → quick-fix that removes the
    duplicate user-declared member.
- Source-generator performance pass (incremental-generator caching audit, benchmark of
  compile-time impact on large solutions).
- Public-API kept stable; any remaining edge-case fixes folded in here so 1.0 is
  bug-fix-only afterwards.

---

## Beyond 1.0

Ideas under consideration once the four phases above ship and the API has settled. None of
these have specs yet; raise an issue if any of them matter to you.

- Multi-value / composite IDs (e.g. `[OrionId<(Guid, int)>]`).
- More built-in strategies (e.g. `Tsid`, `Xid`).
- `IUtf8SpanFormattable` / `IUtf8SpanParsable` emitters for zero-alloc serialization.
- gRPC / protobuf integration emitter.
- A small `OrionKey.SourceLink` companion that lets debuggers step into generated code
  without copying it into the solution.

---

## Contributing & feedback

Issues, design discussions, and PRs are welcome on
[GitHub](https://github.com/tunahanaliozturk/OrionKey). If a phase's scope does not match
your needs, the spec files linked above are the right place to comment — they capture the
design decisions before implementation begins.
