namespace Moongazing.OrionKey.IntegrationTests;

using System;
using Xunit;

// CA1305: these tests deliberately exercise the no-provider convenience Parse overloads
// (MyId.Parse("...")), which is how most consumer code calls them. The inputs used here (a
// canonical Guid string and the digits "12345") parse identically in every culture, so the
// assertions are deterministic despite the culture-default provider.
#pragma warning disable CA1305
public class PublicParseTests
{
    [Fact]
    public void Guid_id_round_trips_through_public_Parse()
    {
        var id = OrderId.New();
        var text = id.ToString();

        Assert.Equal(id, OrderId.Parse(text));
        Assert.Equal(id, OrderId.Parse(text, null));
        Assert.Equal(id, OrderId.Parse(text.AsSpan()));
        Assert.Equal(id, OrderId.Parse(text.AsSpan(), null));
    }

    [Fact]
    public void Long_id_round_trips_through_public_Parse()
    {
        var id = new UserId(12345);

        Assert.Equal(id, UserId.Parse("12345"));
        Assert.Equal(id, UserId.Parse("12345".AsSpan()));
    }

    [Fact]
    public void String_id_round_trips_through_public_Parse()
    {
        Assert.Equal(new TenantId("acme"), TenantId.Parse("acme"));
        Assert.Equal(new TenantId("acme"), TenantId.Parse("acme".AsSpan()));
    }

    [Fact]
    public void Malformed_input_throws_FormatException()
    {
        Assert.Throws<FormatException>(() => OrderId.Parse("not-a-guid"));
        Assert.Throws<FormatException>(() => UserId.Parse("not-a-number"));
    }

    [Fact]
    public void Null_input_throws_ArgumentNullException_for_every_backing()
    {
        Assert.Throws<ArgumentNullException>(() => OrderId.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => UserId.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => TenantId.Parse(null!));
    }

    [Fact]
    public void The_id_parses_through_the_IParsable_interface()
    {
        var parsed = ParseVia<UserId>("12345");
        Assert.Equal(new UserId(12345), parsed);
    }

    private static T ParseVia<T>(string s) where T : IParsable<T>
        => T.Parse(s, null);
}
#pragma warning restore CA1305
