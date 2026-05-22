namespace Moongazing.OrionKey.Tests;

public class OrionGuidComparerTests
{
    [Fact]
    public void CompareV7_ShouldOrderByTimestamp()
    {
        var earlier = GuidV7Factory.NewGuidV7();
        Thread.Sleep(5);
        var later = GuidV7Factory.NewGuidV7();
        Assert.True(OrionGuidComparer.CompareV7(earlier, later) < 0);
        Assert.True(OrionGuidComparer.CompareV7(later, earlier) > 0);
        Assert.Equal(0, OrionGuidComparer.CompareV7(earlier, earlier));
    }

    [Fact]
    public void CompareSequential_ShouldOrderByCreationTime()
    {
        var earlier = SequentialGuidFactory.NewSequentialGuid();
        Thread.Sleep(5);
        var later = SequentialGuidFactory.NewSequentialGuid();
        Assert.True(OrionGuidComparer.CompareSequential(earlier, later) < 0);
        Assert.True(OrionGuidComparer.CompareSequential(later, earlier) > 0);
        Assert.Equal(0, OrionGuidComparer.CompareSequential(earlier, earlier));
    }
}
