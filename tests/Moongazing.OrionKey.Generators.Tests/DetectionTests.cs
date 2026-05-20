namespace Moongazing.OrionKey.Generators.Tests;

public class DetectionTests
{
    [Fact]
    public void Generator_ShouldEmitSource_ForOneArgOrionIdStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>]
            public readonly partial struct OrderId;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.NotEmpty(result.GeneratedSources);
        Assert.Contains("struct OrderId", result.AllGeneratedText());
    }

    [Fact]
    public void Generator_ShouldEmitSource_ForTwoArgOrionIdStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>]
            public readonly partial struct UserId;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.Contains("struct UserId", result.AllGeneratedText());
    }

    [Fact]
    public void Generator_ShouldEmitNothing_ForUndecoratedStruct()
    {
        const string source = """
            namespace Demo;
            public readonly partial struct Plain;
            """;

        var result = GeneratorHarness.Run(source);

        Assert.Empty(result.GeneratedSources);
    }
}
