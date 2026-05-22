namespace Moongazing.OrionKey.Tests;

public class SequentialGuidFactoryTests
{
    [Fact]
    public void NewSequentialGuid_ShouldNotBeEmpty()
    {
        Assert.NotEqual(Guid.Empty, SequentialGuidFactory.NewSequentialGuid());
    }

    [Fact]
    public void NewSequentialGuid_ShouldAscend_UnderSequentialComparer()
    {
        var first = SequentialGuidFactory.NewSequentialGuid();
        Thread.Sleep(5);
        var second = SequentialGuidFactory.NewSequentialGuid();
        Assert.True(OrionGuidComparer.CompareSequential(first, second) < 0);
    }

    [Fact]
    public void NewSequentialGuid_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<Guid>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(SequentialGuidFactory.NewSequentialGuid()));
        }
    }
}
