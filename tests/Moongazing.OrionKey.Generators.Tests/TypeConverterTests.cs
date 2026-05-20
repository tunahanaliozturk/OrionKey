namespace Moongazing.OrionKey.Generators.Tests;

public class TypeConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_TypeConverterAttribute()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("System.ComponentModel.TypeConverter(typeof(OrderIdTypeConverter))", output);
    }

    [Fact]
    public void Emits_TypeConverterClass_WithConvertFromAndTo()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdTypeConverter", output);
        Assert.Contains("CanConvertFrom", output);
        Assert.Contains("ConvertTo", output);
    }
}
