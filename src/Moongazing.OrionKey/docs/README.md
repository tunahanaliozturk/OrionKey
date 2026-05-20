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

The generated converters are discovered automatically by `System.Text.Json`, EF Core, and
ASP.NET Core model binding. No manual registration is required.

OrionKey is released under the MIT License.
