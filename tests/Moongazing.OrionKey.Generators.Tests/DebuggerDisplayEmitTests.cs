namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class DebuggerDisplayEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_DebuggerDisplay_attribute()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("[global::System.Diagnostics.DebuggerDisplay(\"UserId: {Value}\")]",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_struct_emits_DebuggerDisplay_attribute()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.Contains("[global::System.Diagnostics.DebuggerDisplay(\"TenantId: {Value}\")]",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Generator_suppresses_emit_when_consumer_partial_already_declares_DebuggerDisplay()
    {
        // Consumer declares [DebuggerDisplay] on the partial - generator should NOT
        // emit a second one (DebuggerDisplayAttribute is not AllowMultiple).
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [System.Diagnostics.DebuggerDisplay("custom")]
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.DoesNotContain("[global::System.Diagnostics.DebuggerDisplay(\"UserId: {Value}\")]",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_DebuggerDisplay_attribute()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("[global::System.Diagnostics.DebuggerDisplay(\"AccountId: {Value}\")]",
            output, System.StringComparison.Ordinal);
    }
}
