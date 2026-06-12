namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class PublicTryParseSpanEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_public_TryParse_with_ReadOnlySpan_char()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains(
            "public static bool TryParse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider, out UserId result)",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "public static bool TryParse(global::System.ReadOnlySpan<char> s, out UserId result) => TryParse(s, null, out result);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_public_TryParse_span_overload()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains(
            "public static bool TryParse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider, out AccountId result)",
            output, System.StringComparison.Ordinal);
    }
}
