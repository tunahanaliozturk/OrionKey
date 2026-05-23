namespace Moongazing.OrionKey.Generators.Tests;

public class RegistrationEmitterTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Emits_DapperRegistrar_WithOneCallPerId()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);
        Assert.Contains("class OrionKeyDapperRegistrar", output);
        Assert.Contains("global::Dapper.SqlMapper.AddTypeHandler(new global::Demo.OrderIdDapperTypeHandler())", output);
        Assert.Contains("global::Dapper.SqlMapper.AddTypeHandler(new global::Demo.TenantIdDapperTypeHandler())", output);
    }

    [Fact]
    public void Emits_NewtonsoftRegistrar_WithAddToSettings()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """);
        Assert.Contains("class OrionKeyNewtonsoftJsonRegistrar", output);
        Assert.Contains("AddTo(global::Newtonsoft.Json.JsonSerializerSettings settings)", output);
        Assert.Contains("settings.Converters.Add(new global::Demo.OrderIdNewtonsoftJsonConverter())", output);
    }

    [Fact]
    public void Emits_MongoRegistrar_WithRegister()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);
        Assert.Contains("class OrionKeyMongoRegistrar", output);
        Assert.Contains(
            "global::MongoDB.Bson.Serialization.BsonSerializer.RegisterSerializer("
            + "typeof(global::Demo.TenantId), new global::Demo.TenantIdBsonSerializer())",
            output);
    }

    [Fact]
    public void Emits_OpenApiRegistrar_WithAddToOptions()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct UserId;
            """);
        Assert.Contains("class OrionKeyOpenApiRegistrar", output);
        Assert.Contains("AddTo(global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)", output);
        Assert.Contains(
            "global::Microsoft.Extensions.DependencyInjection.SwaggerGenOptionsExtensions"
            + ".SchemaFilter<global::Demo.UserIdSchemaFilter>(options)",
            output);
    }

    [Fact]
    public void DoesNotEmit_AnyRegistrar_WhenNoIdsDeclared()
    {
        var output = Generate("""
            namespace Demo;
            public class Nothing { }
            """);
        Assert.DoesNotContain("OrionKeyDapperRegistrar", output);
        Assert.DoesNotContain("OrionKeyNewtonsoftJsonRegistrar", output);
        Assert.DoesNotContain("OrionKeyMongoRegistrar", output);
        Assert.DoesNotContain("OrionKeyOpenApiRegistrar", output);
    }
}
