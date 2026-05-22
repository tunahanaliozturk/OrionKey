namespace Moongazing.OrionKey.Tests;

public class Cuid2FactoryTests
{
    private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";

    [Fact]
    public void NewCuid2_ShouldBe24Characters()
    {
        Assert.Equal(24, Cuid2Factory.NewCuid2().Length);
    }

    [Fact]
    public void NewCuid2_ShouldStartWithLowercaseLetter()
    {
        var first = Cuid2Factory.NewCuid2()[0];
        Assert.InRange(first, 'a', 'z');
    }

    [Fact]
    public void NewCuid2_ShouldUseOnlyBase36Alphabet()
    {
        Assert.All(Cuid2Factory.NewCuid2(), c => Assert.Contains(c, Base36));
    }

    [Fact]
    public void NewCuid2_ShouldBeUnique_AcrossManyCalls()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 50_000; i++)
        {
            Assert.True(set.Add(Cuid2Factory.NewCuid2()));
        }
    }
}
