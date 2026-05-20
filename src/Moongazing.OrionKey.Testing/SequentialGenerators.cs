namespace Moongazing.OrionKey.Testing;

/// <summary>Produces Snowflake-shaped <see cref="long"/> ids as 1, 2, 3, ... for deterministic tests.</summary>
public sealed class SequentialSnowflake
{
    private long current;

    /// <summary>Returns the next sequential id.</summary>
    public long Next() => System.Threading.Interlocked.Increment(ref current);
}

/// <summary>Produces ascending 26-character ULID-shaped strings for deterministic tests.</summary>
public sealed class SequentialUlid
{
    private long current;

    /// <summary>Returns the next sequential ULID-shaped string.</summary>
    public string Next()
    {
        var n = System.Threading.Interlocked.Increment(ref current);
        return n.ToString("D26", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Produces distinct 21-character NanoId-shaped strings for deterministic tests.</summary>
public sealed class SequentialNanoId
{
    private long current;

    /// <summary>Returns the next sequential NanoId-shaped string.</summary>
    public string Next()
    {
        var n = System.Threading.Interlocked.Increment(ref current);
        return n.ToString("D21", System.Globalization.CultureInfo.InvariantCulture);
    }
}
