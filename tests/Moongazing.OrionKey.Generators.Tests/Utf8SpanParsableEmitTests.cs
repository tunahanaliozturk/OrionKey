namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class Utf8SpanParsableEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_id_implements_IUtf8SpanParsable_under_net8_guard()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("#if NET8_0_OR_GREATER", output, System.StringComparison.Ordinal);
        Assert.Contains("global::System.IUtf8SpanParsable<UserId>", output, System.StringComparison.Ordinal);
        // Guid's UTF-8 parse takes no provider and is called concretely (no boxing).
        Assert.Contains("global::System.Guid.TryParse(utf8, out var v)", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Numeric_backed_id_calls_the_concrete_utf8_parse_with_provider()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("long.TryParse(utf8, provider, out var v)", output, System.StringComparison.Ordinal);
        Assert.DoesNotContain("(global::System.IUtf8SpanParsable<AccountId>)", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_id_decodes_utf8_through_Encoding_UTF8()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.Contains("global::System.Text.Encoding.UTF8.GetString(utf8)", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Public_byte_span_TryParse_overloads_are_emitted()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<int>] public readonly partial struct CounterId;
            """);

        Assert.Contains("public static bool TryParse(global::System.ReadOnlySpan<byte> utf8, out CounterId result)",
            output, System.StringComparison.Ordinal);
        Assert.Contains("#endif", output, System.StringComparison.Ordinal);
    }
}
