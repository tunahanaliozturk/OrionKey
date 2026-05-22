using System.Security.Cryptography;

namespace Moongazing.OrionKey;

/// <summary>
/// Generates CUID2-style ids: 24 lowercase base36 characters, the first always a letter.
/// Collision-resistant and horizontally scalable; not time-sortable.
/// </summary>
public static class Cuid2Factory
{
    private const string Letters = "abcdefghijklmnopqrstuvwxyz";
    private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int Length = 24;

    /// <summary>Generates a new 24-character CUID2 string.</summary>
    public static string NewCuid2()
    {
        Span<char> chars = stackalloc char[Length];
        chars[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        for (var i = 1; i < Length; i++)
        {
            chars[i] = Base36[RandomNumberGenerator.GetInt32(Base36.Length)];
        }
        return new string(chars);
    }
}
