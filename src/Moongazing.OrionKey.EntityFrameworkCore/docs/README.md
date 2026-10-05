# OrionKey.EntityFrameworkCore

Model-wide EF Core value-converter registration for OrionKey strongly-typed IDs: one call wires the converter for every `[OrionId]` property, so you stop configuring ids property by property.

![OrionKey overview: the source generator emits an <Id>ValueConverter per id when the project references EF Core, and OrionKey.EntityFrameworkCore wires those converters across the model](https://raw.githubusercontent.com/tunahanaliozturk/OrionKey/main/docs/diagrams/overview.png)

## Install

    dotnet add package OrionKey.EntityFrameworkCore

It depends on `OrionKey` (the ids and their generator) and on `Microsoft.EntityFrameworkCore` (8.0 on net8.0, 9.0 on net9.0, 10.0 on net10.0); pick any database provider.

OrionKey's generator already emits an `<Id>ValueConverter` and a per-property `HasOrionKeyConversion()` helper for every `[OrionId]` struct when a project references EF Core, but EF Core does not apply them on its own. This package adds the model-wide call.

## Quick start

Call `UseOrionKeyConversions()` once, at the end of `OnModelCreating`, after your entity types are configured. It finds every property whose type carries `[OrionId]` and wires its converter.

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

A property that already has a converter (configured explicitly, or by an earlier call) is left untouched, so explicit configuration always wins over the convention.

## ConfigureConventions

The pre-convention route: `ConfigureOrionKeyConversions()` registers the generated converter for every `[OrionId]` type in the given assemblies (the calling assembly when none are passed).

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.ConfigureOrionKeyConversions(typeof(OrderId).Assembly);
}
```

## Per property

For explicit, trimming- and AOT-safe control, the generic helper takes the id and its underlying primitive:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .HasOrionKeyConversion<OrderId, Guid>();
```

It complements the non-generic `HasOrionKeyConversion()` the generator emits in each id's own namespace.

## AOT and trimming

`UseOrionKeyConversions()` and `ConfigureOrionKeyConversions()` discover id types and their converters by reflection when the model is built, so they are annotated `RequiresUnreferencedCode` / `RequiresDynamicCode`. Under Native AOT or aggressive trimming, use `HasOrionKeyConversion<TId, TValue>()` or the generated per-id `HasOrionKeyConversion()`.

## Related packages

- `OrionKey` - the `[OrionId]` attribute, strategies and source generator.
- `OrionKey.Testing` - `DeterministicIdScope` for repeatable ids in tests.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionKey
- Changelog: https://github.com/tunahanaliozturk/OrionKey/blob/main/CHANGELOG.md
- License: MIT
