# OrionKey

Source-generated strongly-typed IDs for .NET: mark a `readonly partial struct` with one attribute and the bundled Roslyn generator writes its factory, equality, comparison, parsing and serialization members.

![OrionKey overview: an [OrionId] partial struct is compiled by the bundled source generator into always-on companions plus EF Core, Dapper, Newtonsoft.Json, MongoDB and Swashbuckle companions when the project references those libraries; at run time New() calls the OrionKey facade](https://raw.githubusercontent.com/tunahanaliozturk/OrionKey/main/docs/diagrams/overview.png)

## Install

    dotnet add package OrionKey

The source generator, analyzers (`ORIONKEY001`-`ORIONKEY012`) and code fixes ship inside this package; there is nothing else to reference.

## Quick start

```csharp
using Moongazing.OrionKey;

[OrionId<Guid>]                  public readonly partial struct OrderId;
[OrionId<long, Snowflake>]       public readonly partial struct UserId;
[OrionId<string, Ulid>]          public readonly partial struct TenantId;

// Once at startup, before the first Snowflake id: give every instance its own worker id.
OrionKey.Configure(o => o.SnowflakeWorkerId = 7);

var order = OrderId.New();
var user = UserId.New();
bool same = order == OrderId.Parse(order.ToString());   // true

var json = JsonSerializer.Serialize(new { Id = user });  // {"Id":<long>}
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id));
```

`System.Text.Json` (reflection-based) and ASP.NET Core route binding pick up the generated converters without registration. EF Core does not: wire the generated converter with `.Property(o => o.Id).HasOrionKeyConversion()`, or the whole model with the `OrionKey.EntityFrameworkCore` package.

## Strategies

| Declaration | Storage | `New()` | Creation-ordered |
|---|---|---|---|
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
| `[OrionId<int>]` / `[OrionId<long>]` | int / long | none (database identity) | n/a |

Every id implements `IComparable<T>`; creation-ordered strategies compare in creation order, the others by value.

## Snowflake worker id

A Snowflake id is a 41-bit millisecond timestamp (since `SnowflakeEpoch`, default 2025-01-01 UTC), a 10-bit worker id and a 12-bit per-millisecond sequence. The worker id comes from `OrionKeyOptions.SnowflakeWorkerId`, else the `ORIONKEY_WORKER_ID` environment variable (0-1023), else a machine-name hash with a one-time warning. Pin it per instance in any multi-instance deployment. If the clock moves backwards, `NextSnowflake()` throws `OrionKeyClockException` rather than risk a duplicate.

![Snowflake flow: worker id resolution on the first id, then per call the clock check, sequence increment, wait for the next millisecond on overflow, and the OrionKeyClockException failure path](https://raw.githubusercontent.com/tunahanaliozturk/OrionKey/main/docs/diagrams/snowflake-next.png)

## What gets generated

- Always: `Value`, `Empty`, `IsEmpty`, `New()` and `CreateMany(n)` (strategy-backed and Guid ids), equality and `==` / `!=`, `IComparable<T>` with `<` `<=` `>` `>=`, `Parse` / `TryParse` with `IParsable<T>` and `ISpanParsable<T>`, `IUtf8SpanFormattable` / `IUtf8SpanParsable<T>`, a `System.Text.Json` converter, a `TypeConverter`, and `OrionKeyJsonRegistrar.AddTo(options)` for source-generated JSON contexts.
- When the project references the library: EF Core `<Id>ValueConverter` and `HasOrionKeyConversion()`; Dapper `<Id>DapperTypeHandler` (`OrionKeyDapperRegistrar.Register()`); Newtonsoft.Json `<Id>NewtonsoftJsonConverter` (`OrionKeyNewtonsoftJsonRegistrar.AddTo(settings)`); MongoDB `<Id>BsonSerializer` (`OrionKeyMongoRegistrar.Register()`); Swashbuckle `<Id>SchemaFilter` (`OrionKeyOpenApiRegistrar.AddTo(options)`).

## AOT and trimming

`IsAotCompatible`; CI publishes and runs a Native AOT sample on linux-x64 and win-x64. With a source-generated `JsonSerializerContext`, call `OrionKeyJsonRegistrar.AddTo(options)` and construct the context over those options (`new MyJsonContext(options)`), because the per-id `[JsonConverter]` attribute is not visible to the System.Text.Json generator. Newtonsoft.Json, MongoDB.Driver, Swashbuckle.AspNetCore and Dapper themselves are not AOT-clean; prefer System.Text.Json, EF Core and the `TypeConverter` / `IParsable` paths in AOT apps.

Opt-in metrics: set `EnableMetrics = true` in `OrionKey.Configure` to record the `orion.key.ids.generated` counter (tag `strategy`) on the `Moongazing.OrionKey` meter.

## Related packages

- `OrionKey.EntityFrameworkCore` - `UseOrionKeyConversions()` wires every id's EF Core converter in one call.
- `OrionKey.Testing` - `DeterministicIdScope` for repeatable ids in tests.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionKey
- Changelog: https://github.com/tunahanaliozturk/OrionKey/blob/main/CHANGELOG.md
- License: MIT
