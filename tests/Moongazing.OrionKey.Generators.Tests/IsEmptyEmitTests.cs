namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class IsEmptyEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_IsEmpty_against_default()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public bool IsEmpty => Value.Equals(default(global::System.Guid));",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_struct_emits_IsEmpty_via_string_IsNullOrEmpty()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.Contains("public bool IsEmpty => string.IsNullOrEmpty(Value);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_IsEmpty_against_default()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("public bool IsEmpty => Value.Equals(default(long));",
            output, System.StringComparison.Ordinal);
    }
}
