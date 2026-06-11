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
    public void String_backed_struct_emits_ParseOrDefault()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.Contains("public static TenantId? ParseOrDefault(string? s)",
            output, System.StringComparison.Ordinal);
    }
}
