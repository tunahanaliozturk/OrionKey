namespace Moongazing.OrionKey.Tests;

public class ObjectIdFactoryTests
{
    private const string Hex = "0123456789abcdef";

    [Fact]
    public void NewObjectId_ShouldBe24Characters()
    {
        Assert.Equal(24, ObjectIdFactory.NewObjectId().Length);
    }

    [Fact]
    public void NewObjectId_ShouldUseOnlyLowercaseHex()
    {
        Assert.All(ObjectIdFactory.NewObjectId(), c => Assert.Contains(c, Hex));
    }

    [Fact]
    public void NewObjectId_ShouldSortOrdinally_WithinSameSecond()
    {
        var ids = new string[1000];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = ObjectIdFactory.NewObjectId();
        }

        // The 3-byte counter starts at a random value and increments by 1 per call.
        // Across 1000 consecutive ids it can wrap (0xFFFFFF -> 0) at most once, which
        // is the only legitimate cause of a non-ascending adjacent pair.
        var nonAscending = 0;
        for (var i = 1; i < ids.Length; i++)
        {
            if (string.CompareOrdinal(ids[i - 1], ids[i]) >= 0)
            {
                nonAscending++;
            }
        }

        Assert.True(nonAscending <= 1,
            $"Expected at most one counter wrap, found {nonAscending} non-ascending pairs.");
    }

    [Fact]
    public void NewObjectId_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(ObjectIdFactory.NewObjectId()));
        }
    }
}
