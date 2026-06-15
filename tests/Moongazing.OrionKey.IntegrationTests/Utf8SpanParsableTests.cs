namespace Moongazing.OrionKey.IntegrationTests;

using System;
using System.Text;
using Xunit;

public class Utf8SpanParsableTests
{
    [Fact]
    public void Guid_id_round_trips_through_utf8()
    {
        var id = OrderId.New();
        Span<byte> buffer = stackalloc byte[64];
        Assert.True(id.TryFormat(buffer, out var written, default, null));

        Assert.True(OrderId.TryParse(buffer[..written], out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void Long_id_round_trips_through_utf8()
    {
        var id = UserId.New();
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(id.TryFormat(buffer, out var written, default, null));

        Assert.True(UserId.TryParse(buffer[..written], out var parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void String_id_parses_from_utf8()
    {
        var utf8 = Encoding.UTF8.GetBytes("acme");
        Assert.True(TenantId.TryParse(utf8, out var parsed));
        Assert.Equal(new TenantId("acme"), parsed);
    }

    [Fact]
    public void A_malformed_numeric_utf8_returns_false()
    {
        var utf8 = Encoding.UTF8.GetBytes("not-a-number");
        Assert.False(UserId.TryParse(utf8, out _));
    }

    [Fact]
    public void The_id_parses_through_the_IUtf8SpanParsable_interface()
    {
        var utf8 = Encoding.UTF8.GetBytes("12345");
        var parsed = ParseVia<UserId>(utf8);
        Assert.Equal(new UserId(12345), parsed);
    }

    private static T ParseVia<T>(ReadOnlySpan<byte> utf8) where T : IUtf8SpanParsable<T>
        => T.Parse(utf8, null);
}
