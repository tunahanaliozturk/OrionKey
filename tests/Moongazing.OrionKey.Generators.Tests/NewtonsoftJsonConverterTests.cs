namespace Moongazing.OrionKey.Generators.Tests;

public class NewtonsoftJsonConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_Converter_WhenNewtonsoftReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdNewtonsoftJsonConverter", output);
        Assert.Contains("global::Newtonsoft.Json.JsonConverter<OrderId>", output);
    }

    [Fact]
    public void Emits_ReadAndWriteJson_Overrides()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("public override TenantId ReadJson(", output);
        Assert.Contains("public override void WriteJson(", output);
    }

    [Fact]
    public void DoesNotEmit_Converter_WhenNewtonsoftAbsent()
    {
        var result = GeneratorHarness.Run("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, excludeAssemblyNamePrefixes: "Newtonsoft.Json");
        Assert.DoesNotContain("NewtonsoftJsonConverter", result.AllGeneratedText());
    }
}
