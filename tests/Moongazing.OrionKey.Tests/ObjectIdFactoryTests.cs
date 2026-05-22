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
        for (var i = 1; i < ids.Length; i++)
        {
            Assert.True(string.CompareOrdinal(ids[i - 1], ids[i]) < 0,
                $"ObjectId at {i} not strictly greater than predecessor");
        }
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
