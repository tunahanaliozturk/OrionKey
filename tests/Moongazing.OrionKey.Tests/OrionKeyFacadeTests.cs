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

    [Fact]
    public void NewCuid2_ShouldReturn24CharacterString()
    {
        Assert.Equal(24, OrionKey.NewCuid2().Length);
    }

    [Fact]
    public void NewKsuid_ShouldReturn27CharacterString()
    {
        Assert.Equal(27, OrionKey.NewKsuid().Length);
    }

    [Fact]
    public void NewObjectId_ShouldReturn24CharacterString()
    {
        Assert.Equal(24, OrionKey.NewObjectId().Length);
    }

    [Fact]
    public void NewSequentialGuid_ShouldNotReturnEmptyGuid()
    {
        Assert.NotEqual(Guid.Empty, OrionKey.NewSequentialGuid());
    }
}
