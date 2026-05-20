namespace Moongazing.OrionKey.Tests;

[Collection("OrionKeyFacade")]
public class OrionKeyFacadeTests
{
    [Fact]
    public void NextSnowflake_ShouldWork_WithoutExplicitConfigure()
    {
        var a = OrionKey.NextSnowflake();
        var b = OrionKey.NextSnowflake();
        Assert.True(b > a);
    }

    [Fact]
    public void NewUlid_ShouldReturn26Chars()
    {
        Assert.Equal(26, OrionKey.NewUlid().Length);
    }

    [Fact]
    public void NewNanoId_ShouldReturn21Chars()
    {
        Assert.Equal(21, OrionKey.NewNanoId().Length);
    }

    [Fact]
    public void NewGuidV7_ShouldReturnNonEmptyGuid()
    {
        Assert.NotEqual(Guid.Empty, OrionKey.NewGuidV7());
    }
}
