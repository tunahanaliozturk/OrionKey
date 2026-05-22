namespace Moongazing.OrionKey.Tests;

public class KsuidFactoryTests
{
    private const string Base62 =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void NewKsuid_ShouldBe27Characters()
    {
        Assert.Equal(27, KsuidFactory.NewKsuid().Length);
    }

    [Fact]
    public void NewKsuid_ShouldUseOnlyBase62Alphabet()
    {
        Assert.All(KsuidFactory.NewKsuid(), c => Assert.Contains(c, Base62));
    }

    [Fact]
    public void NewKsuid_ShouldSortOrdinallyByCreationOrder()
    {
        var first = KsuidFactory.NewKsuid();
        Thread.Sleep(1100); // KSUID timestamp resolution is one second
        var second = KsuidFactory.NewKsuid();
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void NewKsuid_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(KsuidFactory.NewKsuid()));
        }
    }
}
