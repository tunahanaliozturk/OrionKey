namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class UnwrapAllEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_UnwrapAll_helper()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<global::System.Guid> UnwrapAll(global::System.Collections.Generic.IEnumerable<UserId> ids)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_backed_struct_emits_UnwrapAll_helper()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("public static global::System.Collections.Generic.IReadOnlyList<long> UnwrapAll(global::System.Collections.Generic.IEnumerable<AccountId> ids)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void UnwrapAll_includes_ICollection_capacity_optimization()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("ids is global::System.Collections.Generic.ICollection<UserId> c",
            output, System.StringComparison.Ordinal);
    }
}
