namespace Moongazing.OrionKey.Generators.Tests;

public class ComparableTests
{
    private static string Generate(string attribute, string name)
        => GeneratorHarness.Run($$"""
            using Moongazing.OrionKey;
            namespace Demo;
            [{{attribute}}] public readonly partial struct {{name}};
            """).AllGeneratedText();

    [Fact]
    public void Emits_IComparable_ForSnowflake()
    {
        var output = Generate("OrionId<long, Snowflake>", "UserId");
        Assert.Contains("global::System.IComparable<UserId>", output);
        Assert.Contains("public int CompareTo(UserId other)", output);
        Assert.Contains("operator <(UserId", output);
        Assert.Contains("operator >=(UserId", output);
    }

    [Fact]
    public void Emits_IComparable_ForUlid()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("public int CompareTo(TenantId other)", output);
    }

    // v0.5.26: non-time-ordered ids are now ALSO comparable, with a deterministic
    // (value-based, non-chronological) comparison so they work as SortedSet keys etc.
    [Fact]
    public void NanoId_EmitsValueBased_OrdinalComparison()
    {
        var output = Generate("OrionId<string, NanoId>", "SessionId");
        Assert.Contains("public int CompareTo(SessionId other) => global::System.String.CompareOrdinal(Value, other.Value);", output);
    }

    [Fact]
    public void PlainGuid_EmitsValueBased_GuidComparison()
    {
        var output = Generate("OrionId<System.Guid>", "OrderId");
        Assert.Contains("public int CompareTo(OrderId other) => Value.CompareTo(other.Value);", output);
    }

    [Fact]
    public void Ulid_CompareTo_UsesOrdinalStringComparison()
    {
        var output = Generate("OrionId<string, Ulid>", "TenantId");
        Assert.Contains("global::System.String.CompareOrdinal(Value, other.Value)", output);
    }

    [Fact]
    public void GuidV7_CompareTo_UsesByteOrderComparer()
    {
        var output = Generate("OrionId<System.Guid, GuidV7>", "AuditId");
        Assert.Contains("global::Moongazing.OrionKey.OrionGuidComparer.CompareV7(Value, other.Value)", output);
    }

    [Fact]
    public void SequentialGuid_CompareTo_UsesSequentialComparer()
    {
        var output = Generate("OrionId<System.Guid, SequentialGuid>", "InvoiceId");
        Assert.Contains("global::Moongazing.OrionKey.OrionGuidComparer.CompareSequential(Value, other.Value)", output);
    }

    [Fact]
    public void Emits_IComparable_ForKsuidAndObjectId()
    {
        Assert.Contains("CompareTo(EventId other)", Generate("OrionId<string, Ksuid>", "EventId"));
        Assert.Contains("CompareTo(DocumentId other)", Generate("OrionId<string, ObjectId>", "DocumentId"));
    }

    [Fact]
    public void Cuid2_EmitsValueBased_OrdinalComparison()
    {
        // v0.5.26: Cuid2 (not time-ordered) now emits a deterministic ordinal compare.
        var output = Generate("OrionId<string, Cuid2>", "AccountId");
        Assert.Contains("public int CompareTo(AccountId other) => global::System.String.CompareOrdinal(Value, other.Value);", output);
    }
}
