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

    [Fact]
    public void Emits_DynamicDependency_OnGeneratedStruct()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains(
            "global::System.Diagnostics.CodeAnalysis.DynamicDependency",
            output);
        Assert.Contains(
            "DynamicallyAccessedMemberTypes.PublicConstructors",
            output);
        Assert.Contains(
            "typeof(OrderIdTypeConverter)",
            output);
    }
}
