namespace Moongazing.OrionKey.Generators.Tests;

public class JsonConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_JsonConverterAttribute_OnStruct()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("System.Text.Json.Serialization.JsonConverter(typeof(OrderIdJsonConverter))", output);
    }

    [Fact]
    public void Emits_JsonConverterClass()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdJsonConverter", output);
        Assert.Contains("JsonConverter<OrderId>", output);
    }

    [Fact]
    public void Json_ForLong_UsesNumberApis()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("GetInt64()", output);
        Assert.Contains("WriteNumberValue(", output);
    }

    [Fact]
    public void Json_ForString_UsesStringApis()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("GetString()", output);
        Assert.Contains("WriteStringValue(", output);
    }
}
