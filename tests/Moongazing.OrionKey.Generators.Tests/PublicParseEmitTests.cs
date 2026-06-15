namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class PublicParseEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_public_throwing_Parse_overloads()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains(
            "public static UserId Parse(string s, global::System.IFormatProvider? provider) => new(global::System.Guid.Parse(s));",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "public static UserId Parse(string s) => Parse(s, null);",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "public static UserId Parse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider) => new(global::System.Guid.Parse(s));",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "public static UserId Parse(global::System.ReadOnlySpan<char> s) => Parse(s, null);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_interface_Parse_members_delegate_to_the_public_Parse()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        // A single throwing implementation: the explicit IParsable/ISpanParsable.Parse members
        // forward to the public overloads rather than carrying their own parse expression.
        Assert.Contains(
            "static AccountId global::System.IParsable<AccountId>.Parse(string s, global::System.IFormatProvider? provider) => Parse(s, provider);",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "static AccountId global::System.ISpanParsable<AccountId>.Parse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider) => Parse(s, provider);",
            output, System.StringComparison.Ordinal);
        Assert.Contains(
            "public static AccountId Parse(string s, global::System.IFormatProvider? provider) => new(long.Parse(s, provider));",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_struct_guards_null_with_ArgumentNullException()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        // The string-backed throwing Parse must reject null with ArgumentNullException rather
        // than letting it surface as a NullReferenceException from the constructor call.
        Assert.Contains(
            "public static TenantId Parse(string s, global::System.IFormatProvider? provider) => new((s ?? throw new global::System.ArgumentNullException(nameof(s))).ToString());",
            output, System.StringComparison.Ordinal);
    }
}
