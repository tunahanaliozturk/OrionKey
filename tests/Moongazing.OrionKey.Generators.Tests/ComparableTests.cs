namespace Moongazing.OrionKey.Generators.Tests;

public class ComparableTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_IComparable_ForSnowflake()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("global::System.IComparable<UserId>", output);
        Assert.Contains("public int CompareTo(UserId other)", output);
        Assert.Contains("operator <(UserId", output);
        Assert.Contains("operator >=(UserId", output);
    }

    [Fact]
    public void Emits_IComparable_ForUlid()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("public int CompareTo(TenantId other)", output);
    }

    [Fact]
    public void DoesNotEmit_IComparable_ForNanoId()
    {
        var output = Generate("OrionId<string, NanoId>", "SessionId");
        Assert.DoesNotContain("CompareTo", output);
    }

    [Fact]
    public void DoesNotEmit_IComparable_ForPlainGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.DoesNotContain("CompareTo", output);
    }
}
