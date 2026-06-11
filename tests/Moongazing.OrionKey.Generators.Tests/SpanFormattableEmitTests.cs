namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class SpanFormattableEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Struct_declaration_implements_IFormattable_and_ISpanFormattable()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("global::System.IFormattable", output, System.StringComparison.Ordinal);
        Assert.Contains("global::System.ISpanFormattable", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Guid_backed_id_delegates_ToString_format_to_underlying_Guid()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """);

        Assert.Contains("public string ToString(string? format, global::System.IFormatProvider? formatProvider) => Value.ToString(format, formatProvider);",
            output, System.StringComparison.Ordinal);
        Assert.Contains("((global::System.ISpanFormattable)Value).TryFormat(destination, out charsWritten, format, provider);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_id_delegates_TryFormat_to_underlying_long()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("global::System.ISpanFormattable)Value", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_id_ignores_format_and_emits_safe_TryFormat()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        // String-backed ids do not delegate to ISpanFormattable on string (string does
        // not implement it) - the emit handles the bounds + copy directly.
        Assert.Contains("var source = Value ?? string.Empty;", output, System.StringComparison.Ordinal);
        Assert.Contains("source.AsSpan().CopyTo(destination);", output, System.StringComparison.Ordinal);
    }
}
