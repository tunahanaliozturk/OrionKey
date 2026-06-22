# OrionKey.EntityFrameworkCore

Model-wide EF Core value-converter registration for [OrionKey](https://github.com/tunahanaliozturk/OrionKey)
strongly-typed IDs.

OrionKey's source generator already emits a `ValueConverter<TId, TValue>` and a per-property
`HasOrionKeyConversion()` helper for every `[OrionId]` struct when a project references EF Core. This
package adds the model-wide call so you stop wiring those converters property by property.

## Install

```
dotnet add package OrionKey.EntityFrameworkCore
```

## Model-wide registration

Call `UseOrionKeyConversions()` once, at the end of `OnModelCreating`, after your entity types are
configured. It discovers every `[OrionId]` property on the model and wires its converter.

```csharp
using Moongazing.OrionKey.EntityFrameworkCore;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(b =>
    {
        b.HasKey(o => o.Id);
        // No per-property HasConversion calls needed for OrderId, CustomerId, ...
    });

    modelBuilder.UseOrionKeyConversions();
}
```

An id property that already has a converter (configured explicitly, or by an earlier call) is left
untouched, so explicit configuration always wins over the convention.

## ConfigureConventions

If you prefer the pre-convention route, `ConfigureOrionKeyConversions()` registers the generated
converter for every `[OrionId]` type in the given assemblies (defaulting to the calling assembly):

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.ConfigureOrionKeyConversions(typeof(OrderId).Assembly);
}
```

## Per-property

For explicit, fully trimming- and AOT-safe control, the generic per-property helper takes the id and
its underlying primitive:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .HasOrionKeyConversion<OrderId, Guid>();
```

This complements the `HasOrionKeyConversion()` extension the generator emits in each id's own
namespace.

## AOT and trimming

`UseOrionKeyConversions()` and `ConfigureOrionKeyConversions()` discover id types and their members by
reflection at model-build time, so they are annotated `RequiresUnreferencedCode` /
`RequiresDynamicCode`. Under Native AOT or aggressive trimming, prefer the per-property
`HasOrionKeyConversion<TId, TValue>()` (reflection-free) or the generated per-id converter directly.

## License

MIT. See [LICENSE](https://github.com/tunahanaliozturk/OrionKey/blob/main/LICENSE.txt).
