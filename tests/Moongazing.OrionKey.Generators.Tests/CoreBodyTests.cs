namespace Moongazing.OrionKey.Generators.Tests;

public class CoreBodyTests
{
    private static string Generate(string attribute, string structName)
    {
        var source = $$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{structName}};
            """;
        return GeneratorHarness.Run(source).AllGeneratedText();
    }

    [Fact]
    public void Emits_ValueProperty_AndConstructor_ForGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("public global::System.Guid Value { get; }", output);
        Assert.Contains("public OrderId(global::System.Guid value)", output);
    }

    [Fact]
    public void Emits_IEquatable_AndOperators()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("global::System.IEquatable<OrderId>", output);
        Assert.Contains("operator ==(OrderId", output);
        Assert.Contains("operator !=(OrderId", output);
        Assert.Contains("public override int GetHashCode()", output);
    }

    [Fact]
    public void Emits_New_ForGuid_UsingGuidNewGuid()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("public static OrderId New() => new(global::System.Guid.NewGuid());", output);
    }

    [Fact]
    public void Emits_New_ForSnowflake_DelegatingToFacade()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("global::Moongazing.OrionKey.OrionKey.NextSnowflake()", output);
    }

    [Fact]
    public void DoesNotEmit_New_ForExternallyAssignedInt()
    {
        var output = Generate("OrionId<int>", "LineId");
        Assert.DoesNotContain("New()", output);
    }
}
