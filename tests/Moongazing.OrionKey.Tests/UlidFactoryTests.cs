using Moongazing.OrionKey.Ulid;

namespace Moongazing.OrionKey.Tests;

public class UlidFactoryTests
{
    [Fact]
    public void NewUlid_ShouldBe26Characters()
    {
        Assert.Equal(26, UlidFactory.NewUlid().Length);
    }

    [Fact]
    public void NewUlid_ShouldUseOnlyCrockfordBase32Alphabet()
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        var ulid = UlidFactory.NewUlid();
        Assert.All(ulid, c => Assert.Contains(c, alphabet));
    }

    [Fact]
    public void NewUlid_ShouldSortLexicographicallyByCreationOrder()
    {
        var first = UlidFactory.NewUlid();
        Thread.Sleep(2);
        var second = UlidFactory.NewUlid();
        Assert.True(string.CompareOrdinal(first, second) < 0);
    }

    [Fact]
    public void NewUlid_ShouldBeMonotonic_WithinSameMillisecond()
    {
        var ulids = new string[1000];
        for (var i = 0; i < ulids.Length; i++)
        {
            ulids[i] = UlidFactory.NewUlid();
        }
        for (var i = 1; i < ulids.Length; i++)
        {
            Assert.True(string.CompareOrdinal(ulids[i - 1], ulids[i]) < 0,
                $"ULID at {i} not strictly greater than predecessor");
        }
    }

    [Fact]
    public void NewUlid_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(UlidFactory.NewUlid()));
        }
    }
}
