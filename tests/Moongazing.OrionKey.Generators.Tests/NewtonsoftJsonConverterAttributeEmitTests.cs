namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class NewtonsoftJsonConverterAttributeEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_Newtonsoft_JsonConverter_attribute()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("[global::Newtonsoft.Json.JsonConverter(typeof(UserIdNewtonsoftJsonConverter))]",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_Newtonsoft_JsonConverter_attribute()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("[global::Newtonsoft.Json.JsonConverter(typeof(AccountIdNewtonsoftJsonConverter))]",
            output, System.StringComparison.Ordinal);
    }
}
