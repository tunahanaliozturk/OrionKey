using System.Numerics;
using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates KSUIDs: a 4-byte big-endian seconds-since-epoch timestamp (epoch 2014-05-13)
/// followed by 16 random bytes, base62-encoded to a fixed 27 characters. Lexicographically
/// sortable by creation time under ordinal string comparison.
/// </summary>
public static class KsuidFactory
{
    private const string Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int StringLength = 27;
    private const int PayloadLength = 20;

    private static readonly long Epoch =
        new DateTimeOffset(2014, 5, 13, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>Generates a new 27-character KSUID string.</summary>
    public static string NewKsuid()
    {
        Span<byte> payload = stackalloc byte[PayloadLength];
        var timestamp = (uint)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Epoch);
        payload[0] = (byte)(timestamp >> 24);
        payload[1] = (byte)(timestamp >> 16);
        payload[2] = (byte)(timestamp >> 8);
        payload[3] = (byte)timestamp;
        RandomNumberGenerator.Fill(payload.Slice(4));
        return Encode(payload);
    }

    private static string Encode(ReadOnlySpan<byte> payload)
    {
        var value = new BigInteger(payload, isUnsigned: true, isBigEndian: true);
        Span<char> chars = stackalloc char[StringLength];
        for (var i = StringLength - 1; i >= 0; i--)
        {
            value = BigInteger.DivRem(value, 62, out var remainder);
            chars[i] = Alphabet[(int)remainder];
        }
        return new string(chars);
    }
}
