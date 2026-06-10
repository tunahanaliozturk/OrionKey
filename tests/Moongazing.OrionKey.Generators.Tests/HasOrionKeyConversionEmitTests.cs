namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class HasOrionKeyConversionEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Emit_includes_HasOrionKeyConversion_extension_method()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public static class UserIdEfCoreExtensions", output, System.StringComparison.Ordinal);
        Assert.Contains("HasOrionKeyConversion(", output, System.StringComparison.Ordinal);
        Assert.Contains("new UserIdValueConverter()", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Emit_extension_returns_PropertyBuilder_to_keep_chain()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct OrderId;
            """);

        Assert.Contains("PropertyBuilder<OrderId>", output, System.StringComparison.Ordinal);
        Assert.Contains("return builder.HasConversion(new OrderIdValueConverter());", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Emit_extension_guards_null_builder_argument()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct SkuId;
            """);

        Assert.Contains("throw new global::System.ArgumentNullException(nameof(builder))", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Emit_extension_is_emitted_in_the_same_namespace_as_the_id()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo.Auth;
            [OrionId<System.Guid>] public readonly partial struct AccountId;
            """);

        Assert.Contains("namespace Demo.Auth;", output, System.StringComparison.Ordinal);
        Assert.Contains("public static class AccountIdEfCoreExtensions", output, System.StringComparison.Ordinal);
    }
}
