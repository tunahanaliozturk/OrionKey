using Moongazing.OrionKey.Testing;

namespace Moongazing.OrionKey.Testing.Tests;

[CollectionDefinition("OrionKeyProcessState", DisableParallelization = true)]
public sealed class OrionKeyProcessStateCollection;

[Collection("OrionKeyProcessState")]
public class DeterministicIdScopeTests
{
    [Fact]
    public void Scope_ShouldMakeSnowflakeDeterministic()
    {
        using var scope = new DeterministicIdScope();
        Assert.Equal(1, OrionKey.NextSnowflake());
        Assert.Equal(2, OrionKey.NextSnowflake());
    }

    [Fact]
    public void Scope_ShouldRestoreState_OnDispose()
    {
        long insideScope;
        using (new DeterministicIdScope())
        {
            insideScope = OrionKey.NextSnowflake();
        }
        var afterScope = OrionKey.NextSnowflake();
        Assert.Equal(1, insideScope);
        Assert.True(afterScope > 1_000);
    }

    [Fact]
    public void Scope_ShouldMakeUlidDeterministic()
    {
        using var scope = new DeterministicIdScope();
        var first = OrionKey.NewUlid();
        var second = OrionKey.NewUlid();
        Assert.Equal(26, first.Length);
        Assert.NotEqual(first, second);
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void Scope_ShouldMakeNanoIdDeterministic()
    {
        using var scope = new DeterministicIdScope();
        var first = OrionKey.NewNanoId();
        var second = OrionKey.NewNanoId();
        Assert.Equal(21, first.Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Scope_ShouldMakeGuidV7Deterministic()
    {
        using var scope = new DeterministicIdScope();
        var first = OrionKey.NewGuidV7();
        var second = OrionKey.NewGuidV7();
        Assert.NotEqual(first, second);
        Assert.NotEqual(Guid.Empty, second);
    }
}
