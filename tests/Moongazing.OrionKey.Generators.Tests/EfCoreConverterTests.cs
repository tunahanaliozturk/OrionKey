namespace Moongazing.OrionKey.Generators.Tests;

public class EfCoreConverterTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_ValueConverter_WhenEfCoreReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdValueConverter", output);
        Assert.Contains("ValueConverter<OrderId, global::System.Guid>", output);
    }

    [Fact]
    public void ValueConverter_RoundTripsThroughValue()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("id => id.Value", output);
        Assert.Contains("value => new UserId(value)", output);
    }
}
