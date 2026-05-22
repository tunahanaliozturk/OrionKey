using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates MongoDB-style ObjectIds: a 4-byte big-endian Unix-seconds timestamp, a 5-byte
/// per-process random value, and a 3-byte big-endian counter, rendered as 24 lowercase hex
/// characters. Sortable by creation time under ordinal string comparison.
/// </summary>
public static class ObjectIdFactory
{
    private const int CounterMask = 0xFFFFFF;

    private static readonly byte[] ProcessRandom = CreateProcessRandom();
    private static int counter = RandomNumberGenerator.GetInt32(CounterMask + 1);

    /// <summary>Generates a new 24-character hex ObjectId string.</summary>
    public static string NewObjectId()
    {
        Span<byte> bytes = stackalloc byte[12];
        var timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bytes[0] = (byte)(timestamp >> 24);
        bytes[1] = (byte)(timestamp >> 16);
        bytes[2] = (byte)(timestamp >> 8);
        bytes[3] = (byte)timestamp;
        ProcessRandom.CopyTo(bytes.Slice(4));
        var next = Interlocked.Increment(ref counter) & CounterMask;
        bytes[9] = (byte)(next >> 16);
        bytes[10] = (byte)(next >> 8);
        bytes[11] = (byte)next;
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static byte[] CreateProcessRandom()
    {
        var b = new byte[5];
        RandomNumberGenerator.Fill(b);
        return b;
    }
}
