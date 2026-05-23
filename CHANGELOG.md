# Changelog

All notable changes to OrionKey are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.1] - 2026-05-23

### Changed

- New minimalist family-style key logo in Moongazing indigo (`#312E81`), aligned with the
  sibling OrionGuard, OrionAudit, and OrionLock packages. `docs/icon.png` (NuGet
  `<PackageIcon>`) and `docs/logo.png` (README) both refreshed; transparent background,
  256×256, ~11 KB.
- ROADMAP extended with quarterly milestones through v1.0.0 (Q2 2027), plus *Considered*
  and *Out of scope* sections matching the family-style public roadmaps.

No source or behaviour changes; this is a metadata/asset release.

## [0.3.0] - 2026-05-23

### Added

- Conditional Dapper `SqlMapper.TypeHandler<TId>` emitter — generated whenever the consumer references `Dapper`.
- Conditional Newtonsoft.Json `JsonConverter<TId>` emitter — generated whenever the consumer references `Newtonsoft.Json`.
- Conditional MongoDB `SerializerBase<TId>` emitter (delegates to `BsonSerializer.LookupSerializer<T>()` so user-set Guid representations and driver versions are respected).
- Conditional Swashbuckle `ISchemaFilter` emitter that maps OrionKey ids to their underlying primitive in generated OpenAPI documents.
- Aggregate registrars `OrionKeyDapperRegistrar.Register()`, `OrionKeyNewtonsoftJsonRegistrar.AddTo(...)`, `OrionKeyMongoRegistrar.Register()`, and `OrionKeyOpenApiRegistrar.AddTo(...)` — one call per library wires up every `[OrionId]` struct in the assembly.

### Changed

- Generator internals refactored so all five integration-presence flags live in a single `IntegrationFlags` record passed through the source-output pipeline.

## [0.2.0] - 2026-05-22

### Added

- `Cuid2` strategy — 24-character base36 collision-resistant string ids (`[OrionId<string, Cuid2>]`).
- `Ksuid` strategy — 27-character sortable base62 string ids (`[OrionId<string, Ksuid>]`).
- `ObjectId` strategy — 24-character hex MongoDB-style sortable string ids (`[OrionId<string, ObjectId>]`).
- `SequentialGuid` strategy — index-friendly sortable GUIDs ordered for SQL Server clustered keys (`[OrionId<Guid, SequentialGuid>]`).
- Runtime factories `Cuid2Factory`, `KsuidFactory`, `ObjectIdFactory`, `SequentialGuidFactory`, and `OrionKey` facade members `NewCuid2`/`NewKsuid`/`NewObjectId`/`NewSequentialGuid`.
- `OrionGuidComparer` for byte-order-correct GUID comparison.
- `OrionKey.Testing` sequential generators `SequentialCuid2`, `SequentialKsuid`, `SequentialObjectId`.

### Fixed

- Generated `CompareTo` for GUID-backed sortable ids (`GuidV7`) now compares in byte order, so it preserves creation order; .NET's default `Guid` comparison did not.
- Generated `CompareTo` for string-backed sortable ids (`Ulid`) now uses ordinal comparison instead of culture-sensitive comparison.

## [0.1.0] - 2026-05-20

### Added

- `[OrionId<TValue>]` and `[OrionId<TValue, TStrategy>]` attributes turning a `readonly partial struct` into a strongly-typed id.
- Strategies: `Guid`, `GuidV7`, `Snowflake` (long), `Ulid` (string), `NanoId` (string); `int`/`long` externally-assigned ids.
- Bundled Roslyn incremental generator emitting: struct body with `New()`, `IEquatable`, `IComparable` (sortable strategies), `System.Text.Json` converter, `TypeConverter`, `IParsable`/`ISpanParsable`, and a conditional EF Core `ValueConverter`.
- Runtime ID generators: `SnowflakeIdGenerator`, `UlidFactory`, `NanoIdFactory`, `GuidV7Factory`.
- `OrionKey.Configure` for Snowflake worker-id and epoch; environment-variable and machine-name fallback.
- Diagnostics `ORIONKEY001` through `ORIONKEY005`.
- `OrionKey.Testing` package with `DeterministicIdScope` and sequential generators.
