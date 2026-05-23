namespace Moongazing.OrionKey.Generators.Tests;

public class DapperTypeHandlerTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_TypeHandler_WhenDapperReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdDapperTypeHandler", output);
        Assert.Contains("global::Dapper.SqlMapper.TypeHandler<OrderId>", output);
    }

    [Fact]
    public void Emits_GuidDbType_ForGuidValue()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("DbType.Guid", output);
    }

    [Fact]
    public void Emits_StringDbType_ForStringValue()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("DbType.String", output);
    }

    [Fact]
    public void DoesNotEmit_TypeHandler_WhenDapperAbsent()
    {
        var result = GeneratorHarness.Run("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, excludeAssemblyNamePrefixes: "Dapper");
        Assert.DoesNotContain("DapperTypeHandler", result.AllGeneratedText());
    }
}
