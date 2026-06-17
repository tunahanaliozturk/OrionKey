namespace Moongazing.OrionKey.Demo;

/// <summary>
/// Generated ids implement IFormattable and ISpanFormattable, so they format straight into
/// a stack-allocated span without an intermediate string allocation. This is the path
/// interpolated-string handlers and high-throughput logging take.
/// </summary>
internal static class FormattingDemo
{
    public static void Run()
    {
        DemoConsole.Section("5. Allocation-aware formatting (ISpanFormattable)");

        var user = UserId.New();

        // TryFormat into a caller-owned buffer; no string is allocated by the id.
        Span<char> buffer = stackalloc char[64];
        var ok = user.TryFormat(buffer, out var written, default, null);

        DemoConsole.Item("TryFormat succeeded", ok);
        DemoConsole.Item("chars written", written);
        DemoConsole.Item("formatted span", new string(buffer[..written]));
        DemoConsole.Item("matches ToString()", new string(buffer[..written]) == user.ToString());

        // The same id flows through an interpolated string via the formattable interface.
        DemoConsole.Item("interpolated", $"user-{user}");

        DemoConsole.Note("Snowflake/Guid ids format with zero heap allocation on this path.");
    }
}
