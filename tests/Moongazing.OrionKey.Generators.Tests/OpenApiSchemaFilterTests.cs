namespace Moongazing.OrionKey.Generators.Tests;

public class OpenApiSchemaFilterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_SchemaFilter_WhenSwashbuckleReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdSchemaFilter", output);
        Assert.Contains("ISchemaFilter", output);
    }

    [Fact]
    public void Emits_UuidFormat_ForGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("\"string\"", output);
        Assert.Contains("\"uuid\"", output);
    }

    [Fact]
    public void Emits_Int64Format_ForLong()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("\"integer\"", output);
        Assert.Contains("\"int64\"", output);
    }

    [Fact]
    public void DoesNotEmit_SchemaFilter_WhenSwashbuckleAbsent()
    {
        var result = GeneratorHarness.Run("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, excludeAssemblyNamePrefixes: "Swashbuckle.");
        Assert.DoesNotContain("SchemaFilter", result.AllGeneratedText());
    }
}
