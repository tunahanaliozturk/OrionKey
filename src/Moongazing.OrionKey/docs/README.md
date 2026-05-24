# OrionKey

Source-generated strongly-typed IDs for .NET.

OrionKey turns a `readonly partial struct` into a fully-featured strongly-typed ID with a
single attribute. A bundled Roslyn source generator emits the equality, comparison, factory,
serialization, and persistence members, so a domain ID stops being a bare `Guid` or `long`
and becomes a distinct type the compiler can check. No base class, no runtime reflection,
nothing to wire up.

## Quick start

```
dotnet add package OrionKey
```

Mark a partial struct with `[OrionId]` and a storage type. The optional second type
argument selects a generation strategy:

```csharp
[OrionId<Guid>]              public readonly partial struct OrderId;
[OrionId<long, Snowflake>]   public readonly partial struct UserId;
[OrionId<string, Ulid>]      public readonly partial struct TenantId;
[OrionId<string, NanoId>]    public readonly partial struct SessionId;
```

## Strategies

| Declaration | Storage | New() | Sortable |
|---|---|---|---|
| `[OrionId<Guid>]` | Guid | `Guid.NewGuid()` | no |
| `[OrionId<Guid, GuidV7>]` | Guid | UUIDv7 | yes |
| `[OrionId<Guid, SequentialGuid>]` | Guid | SQL Server-ordered sequential GUID | yes |
| `[OrionId<long, Snowflake>]` | long | Snowflake | yes |
| `[OrionId<string, Ulid>]` | string | ULID | yes |
| `[OrionId<string, Ksuid>]` | string | KSUID | yes |
| `[OrionId<string, ObjectId>]` | string | MongoDB ObjectId (24-char hex) | yes |
| `[OrionId<string, NanoId>]` | string | NanoId | no |
| `[OrionId<string, Cuid2>]` | string | CUID2 | no |
| `[OrionId<int>]` / `[OrionId<long>]` | int/long | none (DB identity) | n/a |

The `int` and `long` integer forms have no `New()` factory; they model ids assigned
externally, typically by a database identity column.

## What gets generated

For every annotated struct the generator emits, as `partial` companions:

- The struct body itself: a `Value` member, a `New()` factory (strategy-backed types), and
  value-based `IEquatable` equality with `==` / `!=`.
- An `IComparable` / `IComparable<T>` implementation, emitted only for sortable strategies
  (`GuidV7`, `SequentialGuid`, `Snowflake`, `Ulid`, `Ksuid`, `ObjectId`).
- A `System.Text.Json` `JsonConverter` so the id serializes as its underlying value.
- A `TypeConverter` for framework conversions and ASP.NET Core model binding.
- `IParsable<T>` and `ISpanParsable<T>` implementations for allocation-aware parsing.
- An EF Core `ValueConverter`, emitted only when the project references EF Core, so the id
  can be used directly as an entity key or property.

The generated converters are discovered automatically by `System.Text.Json`, EF Core, and
ASP.NET Core model binding. No manual registration is required.

## Library integration

When the consumer project references Dapper, Newtonsoft.Json, MongoDB.Driver, or Swashbuckle.AspNetCore, OrionKey emits matching companions for every `[OrionId]` struct:

| Library | Generated companion | One-line registration |
| --- | --- | --- |
| Dapper | `<Id>DapperTypeHandler` | `OrionKeyDapperRegistrar.Register();` |
| Newtonsoft.Json | `<Id>NewtonsoftJsonConverter` | `OrionKeyNewtonsoftJsonRegistrar.AddTo(settings);` |
| MongoDB driver | `<Id>BsonSerializer` | `OrionKeyMongoRegistrar.Register();` |
| Swashbuckle (OpenAPI) | `<Id>SchemaFilter` | `OrionKeyOpenApiRegistrar.AddTo(options);` |

OrionKey is released under the MIT License.

## AOT & trimming

OrionKey 0.4+ is compatible with Native AOT and trimming. The runtime assembly carries `<IsAotCompatible>true</IsAotCompatible>` and a CI matrix publishes a self-contained AOT binary on linux-x64 and win-x64 every push.

Newtonsoft.Json, MongoDB.Driver, and Swashbuckle.AspNetCore are not AOT-clean as of mid-2026 — prefer `System.Text.Json`, EF Core, Dapper, and the BCL `TypeConverter`/`IParsable` pipelines in AOT projects. Two AOT specifics: when using `System.Text.Json` source generation, register the generated `<Id>JsonConverter` instances into `JsonSerializerOptions.Converters` and pass the options into `new JsonContext(options)`; when using Dapper, use `DynamicParameters` instead of anonymous objects for parameter binding.
