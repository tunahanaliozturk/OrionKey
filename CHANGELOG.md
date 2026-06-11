# Changelog

All notable changes to OrionKey are documented in this file. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.11] - 2026-06-11

### Added

#### ORIONKEY005 member-collision code-fix provider

Sixth Phase D code-fix.

- `MemberCollisionCodeFixProvider` in `Moongazing.OrionKey.CodeFixes`.
- Scans the struct at the diagnostic location for `Value`, `New`, `Empty`, `Equals`, `GetHashCode`, `ToString`, `CompareTo` and offers one fix per actually-present collision.
- Handles property, method, and field collisions.
- FixAll via `BatchFixer`.

### Tests

6 new facts.

### Migration from v0.5.10

Source-compatible.

## [0.5.10] - 2026-06-11

### Added

#### `HasOrionKeyConversion()` extension auto-emitted per OrionId

The ORIONKEY006 analyzer (since v0.5.x) reports `HasConversion / HasOrionKeyConversion` as the documented fix, but `HasOrionKeyConversion` itself never shipped - consumers had to write `HasConversion(new {Name}ValueConverter())` manually. v0.5.10 closes that gap by generating the helper alongside the value converter.

- For every `[OrionId]` struct compiled with an EF Core reference, the generator emits:
  - `{Name}ValueConverter` (unchanged from v0.5.9).
  - `{Name}EfCoreExtensions.HasOrionKeyConversion(this PropertyBuilder<{Name}> builder)` extension method that wires the matching converter.
- Return type is `PropertyBuilder<{Name}>` so the call chains: `builder.Property(x => x.Id).HasOrionKeyConversion().IsRequired()`.
- Null-builder argument is guarded.
- Emitted in the same namespace as the id so it is reachable wherever the id type is in scope.

### Tests

4 new emit-snapshot facts.

### Migration from v0.5.9

Source-compatible.

```csharp
builder.Property(x => x.UserId).HasOrionKeyConversion();
```

## [0.5.9] - 2026-06-11

### Added

#### ORIONKEY008 bare-id-promotion code-fix provider

Fifth Phase D code-fix. ORIONKEY008 fires on properties like `public Guid Id` / `public long CustomerId` / `public string SkuId` - bare primitives that should be strongly-typed ids. v0.5.9 ships a quick fix that rewrites the property type AND emits a sibling `[OrionId<TValue>] public readonly partial struct` in the same namespace.

- `BareIdPromotionCodeFixProvider` in `Moongazing.OrionKey.CodeFixes`.
- Naming rule: `Id` -> `{ClassName}Id` (e.g. `Order.Id` -> `OrderId`); `XxxId` -> `XxxId` (e.g. `CustomerId` -> `CustomerId`).
- Value-type mapping: `Guid` / `long` / `int` emit `[OrionId<TValue>]`; `string` emits `[OrionId<string, Ulid>]`.
- Skips emitting the struct when one with the matching name already exists in the file.
- FixAll via `BatchFixer` for solution-wide cleanup of legacy bare-id models.

### Tests

6 new facts.

### Migration from v0.5.8

Source-compatible.

## [0.5.8] - 2026-06-10

### Added

#### ORIONKEY004 incompatible-strategy code-fix provider

Fourth Phase D code-fix. ORIONKEY004 fires when `[OrionId<TValue, TStrategy>]` pairs a strategy with an incompatible value type. v0.5.8 ships a quick fix that rewrites the second type argument.

- `IncompatibleStrategyCodeFixProvider`: `Guid` -> `GuidV7`, `long` / `Int64` -> `Snowflake`, `string` -> `Ulid`.
- Preserves trivia on the strategy arg.
- FixAll via `BatchFixer`.

### Tests

5 new facts.

### Migration from v0.5.7

Source-compatible.

## [0.5.7] - 2026-06-10

### Added

#### ORIONKEY007 unused-OrionId code-fix provider

Third Phase D code-fix. ORIONKEY007 fires when an `[OrionId]` struct is declared but never referenced - dead code that usually means the generator emitted an id type but call sites still use bare `Guid`/`long`/`string`. v0.5.7 adds the "remove the dead declaration" quick fix.

- **`UnusedOrionIdCodeFixProvider`** in `Moongazing.OrionKey.CodeFixes` offers a single quick fix: "Remove unused OrionId struct 'X'". Removes the entire `StructDeclarationSyntax` via `SyntaxRemoveOptions.KeepEndOfLine | KeepExteriorTrivia` so the surrounding namespace formatting and any file-level header comments survive intact.
- FixAll via `WellKnownFixAllProviders.BatchFixer` for solution-wide dead-code cleanup after a refactor.

### Tests

4 new facts: removes the unused struct declaration, preserves surrounding namespace + anchor types, advertises only ORIONKEY007 as fixable, supports BatchFixer for FixAll. 109 facts total in the generator test suite.

### Migration from v0.5.6

Source-compatible. Re-install the OrionKey NuGet to see the new quick fix.

## [0.5.6] - 2026-06-10

### Added

#### `OrionKeyTypeRegistry` - NativeAOT-friendly cross-type dispatch

Framework authors (ASP.NET model binders, route value converters, MVC content-type-aware deserializers) historically had to call `typeof(IdType).GetMethod("Parse")` at request time when handling many OrionId struct types in a single method. That path requires reflection-based metadata, which the trim / NativeAOT analyzer warns about and which is the largest source of trim warnings in framework-level ID-agnostic code.

`OrionKeyTypeRegistry` shifts the decision back to startup time, where consumers register each ID type with strongly-typed delegates the generator already emits (per-struct `IParsable<T>.Parse` + `ToString`). At dispatch time the registry is a `ConcurrentDictionary<Type, (parse, format)>` lookup keyed by `Type` - no reflection, no `MakeGenericType`, no `Activator.CreateInstance`.

- **`Register<TId>(parse, format)`** - idempotent registration; first call wins. Returns `true` when this call performed the registration, `false` when an earlier call already won.
- **`TryParse(Type idType, string value, out object? id)`** - dispatch parsing via the registered delegate; returns `false` for unregistered types or when the delegate throws `FormatException` / `ArgumentException` / `OverflowException`.
- **`Format(Type idType, object id)`** - dispatch formatting; throws when the type is unregistered or when the instance isn't of the expected runtime type.
- **`IsRegistered(Type)`** + **`RegisteredTypes()`** - introspection.
- All implementation paths are AOT-clean (no reflection at the dispatch site; only `Type` equality comparisons).

### Tests

12 new `OrionKeyTypeRegistryTests` facts in a dedicated test collection (parallel-disabled because state is process-global). 62 facts in the runtime test suite.

### Migration from v0.5.5

Source-compatible. Existing consumers don't need to register anything; the registry is opt-in. Framework consumers wire all their ID types once at startup:

```csharp
OrionKeyTypeRegistry.Register<UserId>(s => UserId.Parse(s), id => id.ToString());
OrionKeyTypeRegistry.Register<OrderId>(s => OrderId.Parse(s), id => id.ToString());
// ... then dispatch from a single method:
if (OrionKeyTypeRegistry.TryParse(idType, raw, out var id)) { ... }
```

## [0.5.5] - 2026-06-10

### Changed

#### Source-generator performance pass

End-to-end perf pass for the incremental pipeline. No behaviour change, but the cache is now actually used.

- **Predicate filter narrows up-front**: `predicate: static (node, _) => node is StructDeclarationSyntax` skips class / record / interface candidates before attribute discovery walks them.
- **Cache-friendly pipeline elements**: the transform stage now parses the symbol into a value-equatable `ParsedOrionId` (an `OrionIdModel? + EquatableArray<DiagnosticInfo>` record) instead of returning `INamedTypeSymbol`. Symbols are NOT cache-comparable across compilations - the previous shape busted the cache on every keystroke, re-parsing every `[OrionId]` struct in the solution. `ParsedOrionId` is structural-equal so Roslyn reuses cached outputs whenever the input symbol hasn't changed.
- **Single parse per struct**: both the per-struct emit and the all-models registrar emit consume the SAME parsed pipeline. Previously each struct was parsed twice (once for individual emit, once for the all-models collect). Diagnostics are also produced once.
- **`EquatableArray<T>` for collections**: the all-models collect step returns an `EquatableArray<OrionIdModel>` (structural equality) instead of the default `ImmutableArray<T>` (reference identity), so an unrelated edit doesn't bust the registrar cache.
- **`DiagnosticInfo` value record**: cache-friendly form of `Diagnostic` (descriptor id + location bounds + message args). The emit stage reconstructs a `Diagnostic` at output time. Without this, tunnelling `Diagnostic` instances through the pipeline busted the cache the same way symbols did.
- **`WithTrackingName` on key stages**: `OrionId_OneArg_Parse`, `OrionId_TwoArg_Parse`, and `OrionId_AllModels` are tagged so generator-cache tests can assert specific stages stayed cached across recompiles.

### Tests

2 new `IncrementalCacheTests` facts: identical re-run reuses cached outputs, unrelated source-tree edit does NOT re-run the OrionId transform / all-models collect. All 103 existing tests still pass; 105 total.

### Migration from v0.5.4

Source-compatible. No public API changes. The legacy `OrionIdParser.TryParse(symbol, out model, out diagnostics)` overload is retained for the analyzer surfaces that still consume `Diagnostic` directly; the incremental pipeline now uses the new `OrionIdParser.Parse(symbol)` shape internally.

## [0.5.4] - 2026-06-09

### Added

#### ORIONKEY005 member-collision code-fix provider

Second Phase D code-fix. Source-generator performance pass continues to target v0.5.5.

- **`MemberCollisionCodeFixProvider`** in `Moongazing.OrionKey.CodeFixes` offers one quick fix per colliding member ("Remove user-declared 'X' (generator emits it)") for any of `Value`, `New`, `Empty`, `Equals`, `GetHashCode`, `ToString`, `CompareTo` declared on an `[OrionId]` struct. Removes the declaration via `SyntaxRemoveOptions.KeepNoTrivia` so subsequent compilation lets the source generator emit its own version.
- One distinct `equivalenceKey` per member name so the IDE can show multiple quick fixes when several members collide.
- FixAll via `WellKnownFixAllProviders.BatchFixer` for solution-wide cleanup after a bulk rename or generator upgrade.

### Deferred

- **Source-generator performance pass** -> v0.5.5

### Migration from v0.5.3

Source-compatible. Re-install the OrionKey NuGet to pick up the new code-fix; no build-time behaviour change for existing structs.

## [0.5.3] - 2026-06-09

### Added

#### `Moongazing.OrionKey.CodeFixes` (NEW assembly) — ORIONKEY003 code-fix provider

The first code-fix provider from the Phase D milestone. Quick-fixes for ORIONKEY005 ship in v0.5.4 alongside the source-generator performance pass.

- **`StringStrategyCodeFixProvider`** offers five "Use *<Strategy>* string strategy" quick fixes for the ORIONKEY003 error `string OrionId requires an explicit strategy`. Picks one of `Cuid2`, `Ulid`, `NanoId`, `Ksuid`, or `ObjectId` and rewrites `[OrionId<string>]` into `[OrionId<string, <Strategy>>]` by inserting the second type argument into the attribute's `GenericNameSyntax`. Each fix has a distinct `equivalenceKey` so the IDE can show all five options.
- **FixAll**: uses `WellKnownFixAllProviders.BatchFixer`, so consumers can apply the same strategy to every ORIONKEY003 site in a document, project, or solution in one action.
- **Packaging**: the new `Moongazing.OrionKey.CodeFixes.dll` bundles into the existing `OrionKey` NuGet under `analyzers/dotnet/cs/` alongside the analyzer DLL. IDEs (Visual Studio, Rider, VS Code with the C# extension) pick the code fixes up automatically.

### Deferred

- **ORIONKEY005 code-fix provider** (member-collision quick-fix removing the duplicate user-declared member) -> v0.5.4
- **Source-generator performance pass** -> v0.5.5

### Migration from v0.5.2

Source-compatible. The new assembly is bundled into the OrionKey NuGet; consumers re-install the package (or restore) to see the new quick fixes in their IDE. No build-time behaviour change.

## [0.5.2] - 2026-06-09

### Added

#### ORIONKEY008 — bare `Guid` / `long` / `int` / `string` Id should be promoted

- New `BareIdPromotionAnalyzer` flags a property whose name is `Id` or ends with `Id` (PascalCase) and whose CLR type is `System.Guid`, `long`, `int`, or `string`. Severity is Info; the suggestion encourages promoting to `[OrionId<TValue>]` so primary-key / foreign-key bugs (mixing `OrderId` and `CustomerId` in a method signature) become compile-time errors.
- Nullable value types are unwrapped (`Guid?` matches as `Guid`). Static properties, indexers, and properties already on or already typed as an existing `[OrionId]` struct are skipped to avoid noise.
- Severity respects `.editorconfig` per standard Roslyn conventions, so legacy areas can opt out with a single line:

```ini
[*.cs]
dotnet_diagnostic.ORIONKEY008.severity = none
```

`AnalyzerReleases.Unshipped.md` updated with the new rule for the next graduation pass.

### Deferred

Remaining Phase D items keep their published targets:

- **ORIONKEY003 / ORIONKEY005 code-fix providers** -> v0.5.3
- **Source-generator performance pass** (incremental-generator caching audit + large-solution benchmark) -> v0.5.4

### Migration from v0.5.1

Source-compatible. The new diagnostic surfaces as Info; existing builds see new suggestions but no warnings or errors.

## [0.5.1] - 2026-06-04

### Fixed

#### ORIONKEY007 now fires in generator-equipped builds

The v0.5.0 CHANGELOG recorded a known limitation: ORIONKEY007 (`OrionId struct declared but never referenced`) counted source-generator-emitted partial declarations as references and therefore silently never fired in real consumer builds where the OrionKey source generator runs alongside the analyzer. v0.5.0 unit tests masked the issue because `AnalyzerHarness.RunAsync` only ran the analyzer against user code.

- `UnusedOrionIdAnalyzer` now skips syntax trees whose file path ends in `.g.cs` or `.generated.cs` when counting references. Symbol discovery (the `RegisterSymbolAction` that decides what is *declared*) is unaffected because user-authored `[OrionId]` structs live in source trees, not generator output.
- `AnalyzerHarness` gains `RunWithGeneratedAsync(analyzer, userSources, generatedSources)`. Generator-emitted trees are parsed with `path: "OrionKey_{i}.g.cs"` so analyzers using path-based generated-code detection treat them as generator output.
- New regression test `Generator_emitted_partials_do_not_mask_unused` reproduces the v0.5.0 failure mode and asserts the fix.

### Deferred

The Phase D items previously retargeted from v0.5.0 keep their published targets:

- **ORIONKEY008** (bare `Guid`/`long` `Id` to typed-id suggestion) -> v0.5.2.
- **ORIONKEY003 / ORIONKEY005 code-fix providers** -> v0.5.3.
- **Source-generator performance pass** -> v0.5.4.

### Migration from v0.5.0

Source-compatible. The analyzer's behaviour change manifests as ORIONKEY007 diagnostics that v0.5.0 was silently missing. Severity defaults to Info; consumers can tune via `.editorconfig` per standard Roslyn conventions.

## [0.5.0] - 2026-06-01

### Added

#### Two new analyzers (Phase D, first slice)

- **ORIONKEY006** (Warning) - `OrionId entity key has no EF Core HasConversion call`. Fires on a property of an `[OrionId]`/`[StronglyTypedId]` struct type declared on a class that participates in EF Core mapping when no `HasConversion` (or `HasOrionKeyConversion`) wiring is found in any `IEntityTypeConfiguration<T>.Configure` body. Catches the common bug of generating a typed id, putting it on an entity, and forgetting to register the generated `ValueConverter`. Without the converter, EF maps the struct as an owned type and primary/foreign key behaviour breaks silently.
- **ORIONKEY007** (Info) - `OrionId struct is declared but never referenced`. Fires when a struct decorated with `[OrionId]`/`[StronglyTypedId]` has zero references in the compilation. Catches "I generated the typed id but the call sites still use bare `Guid`/`long`/`string`" cases. Surfaces as a suggestion to keep noise low.

Both analyzers are registered through `WellKnownDiagnosticTags.CompilationEnd` so they aggregate across the whole compilation before reporting. The compilation can still have other errors and the analyzers will run.

#### Analyzer release tracking

- `AnalyzerReleases.Unshipped.md` updated with the two new rules, ready to graduate to `AnalyzerReleases.Shipped.md` on release.

### Known limitation

ORIONKEY007 currently treats generator-emitted partial declarations of an `[OrionId]` struct as references to it. In real consumer builds where the OrionKey source generator runs alongside the analyzer, the diagnostic can under-fire on genuinely unused types. v0.5.1 will harden the check by filtering generated syntax trees from the reference scan and adding a generator-aware test in `AnalyzerHarness`.

### Deferred from v0.5.0

The original v0.5.0 milestone listed four items. Three are de-scoped to keep this minor focused and reviewable:

- **ORIONKEY007 generator-aware reference scan** (known limitation above) -> v0.5.1.
- **ORIONKEY008** (suggestion to promote bare `Guid`/`long` properties named `Id`/`*Id` to typed ids) -> v0.5.1.
- **ORIONKEY003 / ORIONKEY005 code-fix providers** -> v0.5.2.
- **Source-generator performance pass** (incremental-generator caching audit + large-solution benchmark) -> v0.5.3.

`docs/ROADMAP.md` reflects the new targets.

### Migration from v0.4.1

Source-compatible. The new diagnostics surface as Warning / Info and respect `.editorconfig` severity tuning per standard Roslyn analyzer conventions. Consumers that want stricter or laxer behaviour add:

```ini
[*.cs]
dotnet_diagnostic.ORIONKEY006.severity = error
dotnet_diagnostic.ORIONKEY007.severity = none
```

## [0.4.1] - 2026-05-26

### Changed

- Logo now ships with a cream (#F7F1E3) background instead of transparent. Improves contrast against dark-mode README rendering and NuGet package card backgrounds. No functional change.

## [0.4.0] - 2026-05-24

### Added

- `<IsAotCompatible>true</IsAotCompatible>` on the `Moongazing.OrionKey` and `Moongazing.OrionKey.Testing` runtime projects — both are now verified clean under the .NET trim/AOT analyzers.
- Defensive `[DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(<Id>TypeConverter))]` on the generated `<Id>TypeConverter`'s constructor, so the converter's public constructors survive aggressive trimming even on runtimes without the intrinsic `TypeConverterAttribute(Type)` annotation.
- New `sample/Moongazing.OrionKey.AotSample` console app — publishes with `<PublishAot>true</PublishAot>`, round-trips every ID strategy through `System.Text.Json` (with the generated converters registered into `JsonSerializerOptions.Converters` and consumed via a source-generated `JsonSerializerContext`) and `IParsable<T>.Parse` (via the C# 11 static-abstract-interface-member call `T.Parse(text, null)`). The published native binary exits 0 on success.
- New CI job `aot-publish` publishes the AOT sample on `ubuntu-latest`/`linux-x64` and `windows-latest`/`win-x64` and runs the produced binary on every push and PR. The NuGet `publish` job now gates on it, so a broken AOT story blocks the release.

### Changed

- `<Id>JsonConverter` is now emitted as `internal sealed class` instead of `file sealed class`. Internal stays hidden from external consumers, but it is visible to other in-assembly source generators (notably the `System.Text.Json` source generator) — required for AOT scenarios where consumers register the generated converters with a `JsonSerializerContext`.

### Fixed

- `OrionKeyDiagnostics` meter version string (was drifted at `"0.3.0"` across the 0.3.1 release) — now tracks the package version.

### Notes

- Newtonsoft.Json, MongoDB.Driver, and Swashbuckle.AspNetCore are not AOT-clean as of mid-2026. Their Phase B emitters continue to ship and work in non-AOT scenarios; the AOT sample deliberately does not exercise them.
- For AOT projects using OrionKey with `System.Text.Json`: register the generated `<Id>JsonConverter` instances into a `JsonSerializerOptions.Converters` collection and pass that options object to a `JsonSerializerContext` constructor (`new MyJsonContext(options)`), then serialize via `ctx.<TypeName>`. Source generators don't see each other's emitted attributes, so the converter cannot be discovered automatically by the `[JsonSerializable]` attribute alone.
- For AOT projects using OrionKey with Dapper: the OrionKey-generated `<Id>DapperTypeHandler` is AOT-compatible, but the Dapper assembly itself (2.1.35) produces aggregate `IL2104`/`IL3053` warnings during AOT publish — the Dapper team has not yet annotated it as trim-safe. Suppress per-assembly or wait for an upstream Dapper release with annotations. The AOT sample in this repo deliberately omits Dapper for that reason.

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
