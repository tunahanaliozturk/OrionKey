namespace Moongazing.OrionKey.Generators.Tests;

public class ParsableTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_IParsable_AndISpanParsable()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("global::System.IParsable<OrderId>", output);
        Assert.Contains("global::System.ISpanParsable<OrderId>", output);
    }

    [Fact]
    public void Emits_AllFourParseMembers()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("static UserId global::System.IParsable<UserId>.Parse(", output);
        Assert.Contains("static bool global::System.IParsable<UserId>.TryParse(", output);
        Assert.Contains("static UserId global::System.ISpanParsable<UserId>.Parse(", output);
        Assert.Contains("static bool global::System.ISpanParsable<UserId>.TryParse(", output);
    }
}
