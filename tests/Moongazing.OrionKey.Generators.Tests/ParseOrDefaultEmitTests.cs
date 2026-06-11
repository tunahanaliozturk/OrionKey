namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class ParseOrDefaultEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_ParseOrDefault_returning_nullable_struct()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public static UserId? ParseOrDefault(string? s)",
            output, System.StringComparison.Ordinal);
        Assert.Contains("if (string.IsNullOrEmpty(s)) return null;",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_struct_does_NOT_emit_ParseOrDefault()
    {
        // v0.5.21 codex P2 fix: string-backed strategies inherit the generated
        // TryParse(string) that returns true for ANY non-null input. Emitting
        // ParseOrDefault would silently accept malformed input like 'not-a-ulid' and
        // violate the null-on-malformed contract. Suppression is intentional.
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.DoesNotContain("ParseOrDefault",
            output, System.StringComparison.Ordinal);
    }
}
