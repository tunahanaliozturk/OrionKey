namespace Moongazing.OrionKey;

/// <summary>Process-wide OrionKey configuration. Set once via <c>OrionKey.Configure</c>.</summary>
public sealed class OrionKeyOptions
{
    /// <summary>
    /// Snowflake worker id (0..1023). When null, OrionKey derives one from the
    /// <c>ORIONKEY_WORKER_ID</c> environment variable or a machine-name hash.
    /// </summary>
    public int? SnowflakeWorkerId { get; set; }

    /// <summary>Snowflake epoch. Defaults to 2025-01-01 UTC.</summary>
    public DateTime SnowflakeEpoch { get; set; } = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>When true, the <c>orionkey.ids.generated</c> meter counter is recorded. Default false.</summary>
    public bool EnableMetrics { get; set; }
}
