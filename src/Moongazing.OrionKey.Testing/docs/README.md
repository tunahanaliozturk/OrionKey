# OrionKey.Testing

Deterministic ID generation for testing code that uses OrionKey strongly-typed IDs: inside a scope, every strategy-backed `New()` returns 1, 2, 3, ... instead of random or time-based values.

## Install

    dotnet add package OrionKey.Testing

It references `OrionKey`; add it to test projects only.

## Quick start

Wrap the code under test in a `DeterministicIdScope`. While the scope is alive, OrionKey's process-wide facade hands out ascending, repeatable values in each strategy's shape; disposing the scope restores the normal generators.

```csharp
using Moongazing.OrionKey.Testing;

[Fact]
public void Ids_are_repeatable_inside_the_scope()
{
    using var scope = new DeterministicIdScope();

    var first = UserId.New();     // [OrionId<long, Snowflake>]
    var second = UserId.New();

    Assert.Equal(1L, first.Value);
    Assert.Equal(2L, second.Value);
}
```

## What it covers

- Every strategy that goes through the `OrionKey` facade: Snowflake, ULID, NanoId, GuidV7, CUID2, KSUID, ObjectId, SequentialGuid and MonotonicHex. String strategies get zero-padded decimal strings of the strategy's length (MonotonicHex: 32 hex characters).
- Not a plain `[OrionId<Guid>]` id: its `New()` calls `Guid.NewGuid()` directly and stays random.
- Creating and disposing a scope resets the facade, including any `OrionKey.Configure` settings.
- The scope changes process-wide state, so tests that use it must not run in parallel with each other or with other code that generates ids (put them in one non-parallel xUnit collection).

Standalone counters are included for code that takes a generator rather than calling `New()`: `SequentialSnowflake`, `SequentialUlid`, `SequentialNanoId`, `SequentialCuid2`, `SequentialKsuid` and `SequentialObjectId`, each with a thread-safe `Next()`.

## Related packages

- `OrionKey` - the `[OrionId]` attribute, strategies and source generator.
- `OrionKey.EntityFrameworkCore` - model-wide EF Core converter registration.

## Links

- Documentation and full README: https://github.com/tunahanaliozturk/OrionKey
- Changelog: https://github.com/tunahanaliozturk/OrionKey/blob/main/CHANGELOG.md
- License: MIT
