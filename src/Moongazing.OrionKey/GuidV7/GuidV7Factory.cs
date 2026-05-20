using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates version-7 UUIDs (RFC 9562): a 48-bit Unix-millisecond timestamp followed by
/// random bits, sortable by creation time. Uses the BCL implementation on net9.0+ and a
/// conformant polyfill on net8.0.
/// </summary>
public static class GuidV7Factory
{
    /// <summary>Generates a new version-7 GUID.</summary>
#if NET9_0_OR_GREATER
    public static Guid NewGuidV7() => Guid.CreateVersion7();
#else
    public static Guid NewGuidV7()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);  // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);  // variant 0b10

        return new Guid(bytes, bigEndian: true);
    }
#endif
}
