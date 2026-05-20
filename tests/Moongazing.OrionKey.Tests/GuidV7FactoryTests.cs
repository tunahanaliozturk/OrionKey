using Moongazing.OrionKey.GuidV7;

namespace Moongazing.OrionKey.Tests;

public class GuidV7FactoryTests
{
    [Fact]
    public void NewGuidV7_ShouldSetVersionNibbleTo7()
    {
        var guid = GuidV7Factory.NewGuidV7();
        var bytes = guid.ToByteArray();
        var version = (bytes[7] & 0xF0) >> 4;
        Assert.Equal(7, version);
    }

    [Fact]
    public void NewGuidV7_ShouldSortByCreationOrder()
    {
        var first = GuidV7Factory.NewGuidV7();
        Thread.Sleep(2);
        var second = GuidV7Factory.NewGuidV7();
        Assert.True(CompareGuids(first, second) < 0);
    }

    [Fact]
    public void NewGuidV7_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<Guid>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(GuidV7Factory.NewGuidV7()));
        }
    }

    private static int CompareGuids(Guid a, Guid b)
        => string.CompareOrdinal(a.ToString("N"), b.ToString("N"));
}
