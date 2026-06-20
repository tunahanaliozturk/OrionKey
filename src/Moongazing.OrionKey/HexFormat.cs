namespace Moongazing.OrionKey;

/// <summary>
/// Lowercase hex formatting helpers shared by the hex-rendered id strategies.
/// </summary>
/// <remarks>
/// <see cref="Convert.ToHexString(System.ReadOnlySpan{byte})"/> emits uppercase and would
/// require a second <c>ToLowerInvariant()</c> pass, allocating two strings per id. Formatting
/// directly into a stack buffer produces byte-identical lowercase output with exactly one
/// allocation (the result string), which matters on the id-generation hot path.
/// </remarks>
internal static class HexFormat
{
    private const string Digits = "0123456789abcdef";

    /// <summary>
    /// Renders <paramref name="bytes"/> as a lowercase hex string of length
    /// <c>bytes.Length * 2</c> in a single allocation.
    /// </summary>
    public static string ToLowerHex(ReadOnlySpan<byte> bytes)
    {
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[(i * 2)] = Digits[b >> 4];
            chars[(i * 2) + 1] = Digits[b & 0x0F];
        }

        return new string(chars);
    }
}
