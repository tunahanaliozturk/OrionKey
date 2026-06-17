namespace Moongazing.OrionKey.Demo;

/// <summary>Small console helpers so every feature demo renders consistently.</summary>
internal static class DemoConsole
{
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 64));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('=', 64));
    }

    public static void Item(string label, object? value)
        => Console.WriteLine($"  {label,-26}: {value}");

    public static void Note(string text)
        => Console.WriteLine($"  > {text}");
}
