# OrionKey.Testing

Deterministic ID generators for testing code that uses [OrionKey](https://www.nuget.org/packages/OrionKey)
strongly-typed IDs.

OrionKey's default generators produce random or time-based ids, which makes assertions on
generated values awkward. This package swaps them for deterministic, repeatable sequences
so the ids minted under test are predictable.

## Quick start

```
dotnet add package OrionKey.Testing
```

## Usage

Wrap the code under test in a `DeterministicIdScope`. For the lifetime of the scope,
OrionKey's process-wide generators hand out ascending, repeatable ids; disposing the scope
restores the normal generators.

```csharp
using Moongazing.OrionKey.Testing;

using (new DeterministicIdScope())
{
    var first = OrderId.New();
    var second = OrderId.New();
    // first and second are deterministic and ascending
}
```

Because the scope mutates process-wide state, tests that use it must not run in parallel
with each other or with code that generates ids.

OrionKey.Testing is released under the MIT License.
