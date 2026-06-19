namespace Moongazing.OrionKey.Tests;

public class MonotonicHexFactoryTests
{
    [Fact]
    public void NewMonotonicHex_ShouldBe32LowercaseHexCharacters()
    {
        var id = MonotonicHexFactory.NewMonotonicHex();
        Assert.Equal(32, id.Length);
        Assert.All(id, c => Assert.True(Uri.IsHexDigit(c) && !char.IsUpper(c)));
        Assert.Equal(16, Convert.FromHexString(id).Length);
    }

    [Fact]
    public void NewMonotonicHex_ShouldSortLexicographicallyByCreationOrder()
    {
        var first = MonotonicHexFactory.NewMonotonicHex();
        Thread.Sleep(2);
        var second = MonotonicHexFactory.NewMonotonicHex();
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void NewMonotonicHex_ShouldBeMonotonic_WithinSameMillisecond()
    {
        var ids = new string[2000];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = MonotonicHexFactory.NewMonotonicHex();
        }
        for (var i = 1; i < ids.Length; i++)
        {
            Assert.True(string.CompareOrdinal(ids[i - 1], ids[i]) < 0,
                $"id at {i} not strictly greater than predecessor");
        }
    }

    [Fact]
    public void NewMonotonicHex_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(MonotonicHexFactory.NewMonotonicHex()));
        }
    }
}
