using System.Security.Cryptography;

namespace Moongazing.OrionKey.Ulid;

/// <summary>
/// Generates ULIDs: a 48-bit millisecond timestamp followed by 80 bits of randomness,
/// encoded as 26 Crockford base32 characters. Monotonic within a millisecond.
/// </summary>
public static class UlidFactory
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object Gate = new();
    private static long lastTimestamp = -1;
    private static readonly byte[] LastRandomness = new byte[10];

    /// <summary>Generates a new 26-character ULID string.</summary>
    public static string NewUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> randomness = stackalloc byte[10];

        lock (Gate)
        {
            if (timestamp == lastTimestamp)
            {
                IncrementBigEndian(LastRandomness);
            }
            else
            {
                RandomNumberGenerator.Fill(LastRandomness);
                lastTimestamp = timestamp;
            }
            LastRandomness.CopyTo(randomness);
        }

        return Encode(timestamp, randomness);
    }

    private static void IncrementBigEndian(byte[] bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            if (++bytes[i] != 0)
            {
                return;
            }
        }
    }

    private static string Encode(long timestamp, ReadOnlySpan<byte> randomness)
    {
        Span<char> chars = stackalloc char[26];

        for (var i = 9; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(timestamp & 0x1F)];
            timestamp >>= 5;
        }

        var bitBuffer = 0;
        var bitCount = 0;
        var outIndex = 10;
        foreach (var b in randomness)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                chars[outIndex++] = Alphabet[(bitBuffer >> bitCount) & 0x1F];
            }
        }

        return new string(chars);
    }
}
