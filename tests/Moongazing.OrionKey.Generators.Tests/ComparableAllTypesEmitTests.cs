namespace Moongazing.OrionKey.Generators.Tests;

using Xunit;

public class ComparableAllTypesEmitTests
{
    private static string Generate(string source) => GeneratorHarness.Run(source).AllGeneratedText();

    [Fact]
    public void Random_Guid_id_now_emits_IComparable_with_value_comparison()
    {
        // Plain Guid (no time-ordered strategy) was NOT comparable before v0.5.26.
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            """);

        Assert.Contains("global::System.IComparable<UserId>", output, System.StringComparison.Ordinal);
        Assert.Contains("public int CompareTo(UserId other) => Value.CompareTo(other.Value);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void String_backed_NanoId_emits_ordinal_value_comparison()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, NanoId>] public readonly partial struct TokenId;
            """);

        Assert.Contains("public int CompareTo(TokenId other) => global::System.String.CompareOrdinal(Value, other.Value);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Int_backed_id_emits_IComparable()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<int>] public readonly partial struct SeatId;
            """);

        Assert.Contains("global::System.IComparable<SeatId>", output, System.StringComparison.Ordinal);
        Assert.Contains("public int CompareTo(SeatId other) => Value.CompareTo(other.Value);",
            output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Sortable_Ulid_keeps_time_ordered_comparison()
    {
        var output = Generate("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ulid>] public readonly partial struct EventId;
            """);

        // Ulid is a sortable strategy - ordinal compare gives chronological order.
        Assert.Contains("public int CompareTo(EventId other) => global::System.String.CompareOrdinal(Value, other.Value);",
            output, System.StringComparison.Ordinal);
    }
}
