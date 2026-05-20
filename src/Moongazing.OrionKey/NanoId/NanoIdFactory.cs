using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates NanoIds: 21 characters drawn uniformly from a 64-character URL-safe alphabet,
/// sourced from a cryptographic random number generator.
/// </summary>
public static class NanoIdFactory
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";
    private const int Size = 21;

    /// <summary>Generates a new 21-character NanoId string.</summary>
    public static string NewNanoId()
    {
        Span<byte> bytes = stackalloc byte[Size];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[Size];
        for (var i = 0; i < Size; i++)
        {
            chars[i] = Alphabet[bytes[i] & 63];
        }
        return new string(chars);
    }
}
