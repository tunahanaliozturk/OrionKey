namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class WrapAllEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_WrapAll_helper()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<UserId> WrapAll(global::System.Collections.Generic.IEnumerable<global::System.Guid> values)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_struct_emits_WrapAll_helper()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct TenantId;
            """);

        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<TenantId> WrapAll(global::System.Collections.Generic.IEnumerable<string> values)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_WrapAll_helper()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<AccountId> WrapAll(global::System.Collections.Generic.IEnumerable<long> values)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void WrapAll_includes_ICollection_capacity_optimization()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("global::System.Collections.Generic.ICollection<global::System.Guid> c",
            output, System.StringComparison.Ordinal);
    }
}
