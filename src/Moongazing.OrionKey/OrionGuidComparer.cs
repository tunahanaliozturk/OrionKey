namespace Moongazing.OrionKey;

/// <summary>
/// Byte-order-correct comparison for GUID-backed sortable id strategies. .NET's built-in
/// <see cref="Guid"/> comparison is field-by-field in a non-byte order and does not preserve
/// creation order, so generated <c>CompareTo</c> for <c>GuidV7</c> and <c>SequentialGuid</c>
/// delegates here.
/// </summary>
public static class OrionGuidComparer
{
    /// <summary>Compares two version-7 GUIDs in RFC 9562 big-endian byte order.</summary>
    public static int CompareV7(Guid left, Guid right)
    {
        Span<byte> a = stackalloc byte[16];
        Span<byte> b = stackalloc byte[16];
        left.TryWriteBytes(a, bigEndian: true, out _);
        right.TryWriteBytes(b, bigEndian: true, out _);
        return a.SequenceCompareTo(b);
    }

    /// <summary>
    /// Compares two sequential GUIDs in SQL Server <c>uniqueidentifier</c> sort order. The
    /// creation timestamp occupies bytes 10-15, which SQL Server treats as most significant.
    /// </summary>
    public static int CompareSequential(Guid left, Guid right)
    {
        Span<byte> a = stackalloc byte[16];
        Span<byte> b = stackalloc byte[16];
        left.TryWriteBytes(a);
        right.TryWriteBytes(b);
        var byTimestamp = a.Slice(10).SequenceCompareTo(b.Slice(10));
        return byTimestamp != 0
            ? byTimestamp
            : a.Slice(0, 10).SequenceCompareTo(b.Slice(0, 10));
    }
}
