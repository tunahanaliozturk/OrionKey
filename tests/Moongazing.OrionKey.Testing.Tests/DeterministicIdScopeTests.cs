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
}
