namespace Moongazing.OrionKey.Demo;

/// <summary>
/// Sortable strategies (Snowflake, GuidV7, SequentialGuid, ULID, KSUID, ObjectId) emit
/// IComparable, so ids minted later sort after ids minted earlier. That is what makes
/// them index-friendly clustered keys.
/// </summary>
internal static class OrderingDemo
{
    public static void Run()
    {
        DemoConsole.Section("3. Time-ordered ids (IComparable)");

        // Mint a handful in sequence; they should already be in ascending order.
        var ids = UserId.CreateMany(5);

        var sorted = ids.OrderBy(x => x).ToArray();
        var alreadyOrdered = ids.SequenceEqual(sorted);

        for (var i = 0; i < ids.Length; i++)
        {
            DemoConsole.Item($"UserId[{i}]", ids[i]);
        }

        DemoConsole.Item("minted in ascending order", alreadyOrdered);
        DemoConsole.Item("first < last", ids[0] < ids[^1]);

        // Same property holds for the sortable string strategy.
        var t1 = TenantId.New();
        var t2 = TenantId.New();
        DemoConsole.Item("ULID t1 < t2", string.CompareOrdinal(t1.Value, t2.Value) < 0);

        DemoConsole.Note("CreateMany(n) pre-allocates the array instead of a LINQ loop.");
    }
}
