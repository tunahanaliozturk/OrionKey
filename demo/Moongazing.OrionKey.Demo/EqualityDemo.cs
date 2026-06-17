namespace Moongazing.OrionKey.Demo;

/// <summary>
/// Strongly-typed ids are value types with generated value-equality. Two ids are equal
/// when their underlying values are equal, and they slot straight into hash-based
/// collections. The compiler also refuses to mix one id type with another.
/// </summary>
internal static class EqualityDemo
{
    public static void Run()
    {
        DemoConsole.Section("2. Value equality, hashing, and Empty");

        var a = OrderId.New();
        var b = a;                 // copy of the same value
        var c = OrderId.New();     // a different value

        DemoConsole.Item("a", a);
        DemoConsole.Item("b (copy of a)", b);
        DemoConsole.Item("c (fresh)", c);
        DemoConsole.Item("a == b", a == b);
        DemoConsole.Item("a == c", a == c);
        DemoConsole.Item("a.Equals(b)", a.Equals(b));
        DemoConsole.Item("same hash a/b", a.GetHashCode() == b.GetHashCode());

        // Works as a dictionary key and set member out of the box.
        var seen = new HashSet<OrderId> { a, b, c };
        DemoConsole.Item("distinct ids in {a,b,c}", seen.Count);

        var balances = new Dictionary<UserId, decimal>();
        var user = UserId.New();
        balances[user] = 100.50m;
        DemoConsole.Item("dictionary lookup", balances[user]);

        // Empty and IsEmpty model the "no id yet" state without nullable wrappers.
        DemoConsole.Item("OrderId.Empty", OrderId.Empty);
        DemoConsole.Item("OrderId.Empty.IsEmpty", OrderId.Empty.IsEmpty);
        DemoConsole.Item("fresh id IsEmpty", a.IsEmpty);

        DemoConsole.Note("OrderId and UserId are distinct types: 'OrderId == UserId' would not compile.");
    }
}
