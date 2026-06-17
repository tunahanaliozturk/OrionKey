using System.Globalization;

namespace Moongazing.OrionKey.Demo;

/// <summary>
/// The generator emits IParsable / ISpanParsable (Parse / TryParse) plus a primitive-only
/// ParseOrDefault helper. Round-tripping an id through its string form and back yields an
/// equal value, which is exactly what route binding and serialization rely on.
/// </summary>
internal static class ParsingDemo
{
    public static void Run()
    {
        DemoConsole.Section("4. Parse / TryParse / round-trip");

        // Guid-backed round-trip.
        var order = OrderId.New();
        var orderText = order.ToString();
        var parsedOrder = OrderId.Parse(orderText);
        DemoConsole.Item("OrderId text", orderText);
        DemoConsole.Item("Parse round-trip equal", order == parsedOrder);

        // Snowflake (long) round-trip via Parse(string, IFormatProvider).
        var user = UserId.New();
        var reparsedUser = UserId.Parse(user.ToString(), CultureInfo.InvariantCulture);
        DemoConsole.Item("UserId round-trip equal", user == reparsedUser);

        // TryParse never throws.
        var ok = OrderId.TryParse(orderText, out var viaTry);
        DemoConsole.Item("TryParse(valid) -> ok", ok);
        DemoConsole.Item("TryParse(valid) value", viaTry);

        var bad = OrderId.TryParse("not-a-guid", out _);
        DemoConsole.Item("TryParse(garbage) -> ok", bad);

        // ParseOrDefault (emitted for primitive-backed ids only) returns null on bad input
        // instead of throwing -- handy for query-string parsing.
        DemoConsole.Item("ParseOrDefault(valid)", UserId.ParseOrDefault(user.ToString()));
        DemoConsole.Item("ParseOrDefault(garbage)",
            UserId.ParseOrDefault("xyz") is null ? "null" : "unexpected");

        // String-backed ids round-trip through their own value.
        var tenant = TenantId.New();
        var reparsedTenant = TenantId.Parse(tenant.ToString());
        DemoConsole.Item("ULID round-trip equal", tenant == reparsedTenant);
    }
}
