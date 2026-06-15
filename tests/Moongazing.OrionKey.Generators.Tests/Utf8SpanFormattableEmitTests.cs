namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class Utf8SpanFormattableEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_id_implements_IUtf8SpanFormattable_under_net8_guard()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("#if NET8_0_OR_GREATER", output, System.StringComparison.Ordinal);
        Assert.Contains("global::System.IUtf8SpanFormattable", output, System.StringComparison.Ordinal);
        Assert.Contains("public bool TryFormat(global::System.Span<byte> utf8Destination, out int bytesWritten",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Numeric_backed_id_calls_the_concrete_overload_without_boxing()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        // Must NOT box to IUtf8SpanFormattable (that allocates and defeats the purpose):
        // call the concrete long.TryFormat overload directly, with the provider.
        Assert.Contains("return Value.TryFormat(utf8Destination, out bytesWritten, format, provider);",
            output, System.StringComparison.Ordinal);
        Assert.DoesNotContain("(global::System.IUtf8SpanFormattable)Value", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Guid_backed_id_calls_the_concrete_overload_without_provider()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """);

        // Guid's UTF-8 TryFormat overload takes no IFormatProvider.
        Assert.Contains("return Value.TryFormat(utf8Destination, out bytesWritten, format);",
            output, System.StringComparison.Ordinal);
        Assert.DoesNotContain("(global::System.IUtf8SpanFormattable)Value", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_id_utf8_encodes_through_Encoding_UTF8()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        // String does not implement IUtf8SpanFormattable - the emit UTF-8 encodes directly.
        Assert.Contains("global::System.Text.Encoding.UTF8.TryGetBytes(source, utf8Destination, out bytesWritten);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void The_guarded_block_is_closed_with_endif()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<int>] public readonly partial struct CounterId;
            """);

        Assert.Contains("#endif", output, System.StringComparison.Ordinal);
    }
}
