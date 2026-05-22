using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates sequential GUIDs whose byte layout makes them ascending under SQL Server's
/// <c>uniqueidentifier</c> sort order, keeping clustered-index inserts append-mostly. The
/// last six bytes hold a big-endian Unix-millisecond timestamp; the first ten are random.
/// </summary>
public static class SequentialGuidFactory
{
    /// <summary>Generates a new index-friendly sequential GUID.</summary>
    public static Guid NewSequentialGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes.Slice(0, 10));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[10] = (byte)(timestamp >> 40);
        bytes[11] = (byte)(timestamp >> 32);
        bytes[12] = (byte)(timestamp >> 24);
        bytes[13] = (byte)(timestamp >> 16);
        bytes[14] = (byte)(timestamp >> 8);
        bytes[15] = (byte)timestamp;
        return new Guid(bytes);
    }
}
