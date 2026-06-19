using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates time-ordered ids rendered as 32 lowercase hex characters: a 48-bit millisecond
/// Unix timestamp (big-endian) followed by 80 bits of randomness. The layout and the
/// <c>0-9a-f</c> alphabet make ordinal string comparison equal chronological order, and ids
/// minted in the same millisecond are strictly increasing within the process (the 80-bit
/// randomness block is incremented rather than re-drawn), so a sequence of ids is monotonic.
/// </summary>
/// <remarks>
/// The 16-byte big-endian layout mirrors ULID's timestamp-then-randomness ordering but uses a
/// hex alphabet so the output is a valid lowercase hex string (round-trippable through
/// <c>Convert.FromHexString</c>) and is directly usable as a key in hex-sorted stores.
/// Generation is thread-safe and the monotonic counter is shared across all callers.
/// </remarks>
public static class MonotonicHexFactory
{
    private static readonly object Gate = new();
    private static long lastTimestamp = -1;
    private static readonly byte[] LastRandomness = new byte[10];

    /// <summary>Generates a new 32-character lowercase-hex, time-ordered id string.</summary>
    /// <returns>A 32-character lowercase hex string whose ordinal order matches creation order.</returns>
    public static string NewMonotonicHex()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> bytes = stackalloc byte[16];

        lock (Gate)
        {
            if (timestamp == lastTimestamp)
            {
                IncrementBigEndian(LastRandomness);
            }
            else if (timestamp < lastTimestamp)
            {
                // Clock moved backwards (NTP step / leap second). Hold the previous timestamp
                // and increment the randomness so the sequence stays monotonic rather than
                // emitting an id that sorts before its predecessor.
                timestamp = lastTimestamp;
                IncrementBigEndian(LastRandomness);
            }
            else
            {
                RandomNumberGenerator.Fill(LastRandomness);
                lastTimestamp = timestamp;
            }

            // 48-bit timestamp big-endian into bytes[0..6).
            bytes[0] = (byte)(timestamp >> 40);
            bytes[1] = (byte)(timestamp >> 32);
            bytes[2] = (byte)(timestamp >> 24);
            bytes[3] = (byte)(timestamp >> 16);
            bytes[4] = (byte)(timestamp >> 8);
            bytes[5] = (byte)timestamp;
            LastRandomness.CopyTo(bytes.Slice(6));
        }

        // Convert.ToHexString is available on net8+ and is used with ToLowerInvariant() so the
        // output is deterministic regardless of multi-target / culture (no net9+-only API).
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    // Kept for parity with the binary layout helpers; not currently on a hot path but documents
    // the big-endian timestamp contract for future maintainers.
    internal static long ReadTimestamp(ReadOnlySpan<byte> bytes)
    {
        Span<byte> wide = stackalloc byte[8];
        bytes.Slice(0, 6).CopyTo(wide.Slice(2));
        return BinaryPrimitives.ReadInt64BigEndian(wide);
    }
}
