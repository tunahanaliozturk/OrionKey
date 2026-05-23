namespace Moongazing.OrionKey.Generators.Tests;

public class MongoBsonSerializerTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_Serializer_WhenMongoReferenced()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("class OrderIdBsonSerializer", output);
        Assert.Contains("SerializerBase<OrderId>", output);
    }

    [Fact]
    public void Emits_SerializeAndDeserialize_Overrides()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("public override TenantId Deserialize(", output);
        Assert.Contains("public override void Serialize(", output);
    }

    [Fact]
    public void DoesNotEmit_Serializer_WhenMongoAbsent()
    {
        var result = GeneratorHarness.Run("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, excludeAssemblyNamePrefixes: "MongoDB.");
        Assert.DoesNotContain("BsonSerializer", result.AllGeneratedText());
    }
}
