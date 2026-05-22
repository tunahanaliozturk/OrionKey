using Moongazing.OrionKey.Testing;

namespace Moongazing.OrionKey.Testing.Tests;

public class SequentialGeneratorsTests
{
    [Fact]
    public void SequentialSnowflake_ShouldProduceAscendingValuesFromOne()
    {
        var gen = new SequentialSnowflake();
        Assert.Equal(1, gen.Next());
        Assert.Equal(2, gen.Next());
        Assert.Equal(3, gen.Next());
    }

    [Fact]
    public void SequentialUlid_ShouldProduce26CharAscendingValues()
    {
        var gen = new SequentialUlid();
        var first = gen.Next();
        var second = gen.Next();
        Assert.Equal(26, first.Length);
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void SequentialNanoId_ShouldProduce21CharDistinctValues()
    {
        var gen = new SequentialNanoId();
        Assert.NotEqual(gen.Next(), gen.Next());
        Assert.Equal(21, new SequentialNanoId().Next().Length);
    }

    [Fact]
    public void SequentialCuid2_ShouldProduce24CharDistinctValues()
    {
        var gen = new SequentialCuid2();
        Assert.NotEqual(gen.Next(), gen.Next());
        Assert.Equal(24, new SequentialCuid2().Next().Length);
    }

    [Fact]
    public void SequentialKsuid_ShouldProduce27CharAscendingValues()
    {
        var gen = new SequentialKsuid();
        var first = gen.Next();
        var second = gen.Next();
        Assert.Equal(27, first.Length);
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void SequentialObjectId_ShouldProduce24CharAscendingValues()
    {
        var gen = new SequentialObjectId();
        var first = gen.Next();
        var second = gen.Next();
        Assert.Equal(24, first.Length);
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }
}
