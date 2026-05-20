namespace Moongazing.OrionKey.Tests;

public class NanoIdFactoryTests
{
    [Fact]
    public void NewNanoId_ShouldBe21Characters()
    {
        Assert.Equal(21, NanoIdFactory.NewNanoId().Length);
    }

    [Fact]
    public void NewNanoId_ShouldUseOnlyUrlSafeAlphabet()
    {
        const string alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
        var id = NanoIdFactory.NewNanoId();
        Assert.All(id, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void NewNanoId_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 100_000; i++)
        {
            Assert.True(set.Add(NanoIdFactory.NewNanoId()));
        }
    }
}
