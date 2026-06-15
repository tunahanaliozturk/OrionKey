namespace Moongazing.OrionKey.IntegrationTests;

using System;
using System.Text;
using Xunit;

public class Utf8SpanFormattableTests
{
    [Fact]
    public void Guid_id_formats_to_utf8_matching_ToString()
    {
        var id = OrderId.New();
        Span<byte> buffer = stackalloc byte[64];

        Assert.True(id.TryFormat(buffer, out var written, default, null));
        Assert.Equal(id.ToString(), Encoding.UTF8.GetString(buffer[..written]));
    }

    [Fact]
    public void Long_id_formats_to_utf8_matching_ToString()
    {
        var id = UserId.New();
        Span<byte> buffer = stackalloc byte[32];

        Assert.True(id.TryFormat(buffer, out var written, default, null));
        Assert.Equal(id.ToString(), Encoding.UTF8.GetString(buffer[..written]));
    }

    [Fact]
    public void String_id_utf8_encodes_its_value()
    {
        var id = new TenantId("acme");
        Span<byte> buffer = stackalloc byte[16];

        Assert.True(id.TryFormat(buffer, out var written, default, null));
        Assert.Equal("acme", Encoding.UTF8.GetString(buffer[..written]));
    }

    [Fact]
    public void A_too_small_buffer_returns_false()
    {
        var id = OrderId.New();
        Span<byte> tiny = stackalloc byte[4];

        Assert.False(id.TryFormat(tiny, out var written, default, null));
        Assert.Equal(0, written);
    }

    [Fact]
    public void The_id_is_usable_through_the_IUtf8SpanFormattable_interface()
    {
        // Passing the id where an IUtf8SpanFormattable is expected proves the interface is
        // implemented - which is the whole point of this test.
        Assert.True(WriteVia(UserId.New()) > 0);
    }

#pragma warning disable CA1859 // the interface parameter type is the point of this test
    private static int WriteVia(IUtf8SpanFormattable formattable)
#pragma warning restore CA1859
    {
        Span<byte> buffer = stackalloc byte[32];
        Assert.True(formattable.TryFormat(buffer, out var written, default, null));
        return written;
    }
}
