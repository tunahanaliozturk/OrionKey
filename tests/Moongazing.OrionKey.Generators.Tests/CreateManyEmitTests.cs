namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class CreateManyEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Guid_backed_struct_emits_CreateMany_factory()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("public static UserId[] CreateMany(int count)",
            output, System.StringComparison.Ordinal);
        Assert.Contains("arr[i] = New();",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Long_strategy_struct_emits_CreateMany_factory()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct AccountId;
            """);

        Assert.Contains("public static AccountId[] CreateMany(int count)",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void CreateMany_throws_on_negative_count()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("throw new global::System.ArgumentOutOfRangeException(nameof(count))",
            output, System.StringComparison.Ordinal);
    }
}
