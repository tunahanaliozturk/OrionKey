namespace Moongazing.OrionKey;

/// <summary>How a Snowflake worker id was determined.</summary>
public enum WorkerIdSource
{
    /// <summary>Set explicitly via <see cref="OrionKeyOptions.SnowflakeWorkerId"/>.</summary>
    Explicit,
    /// <summary>Read from the <c>ORIONKEY_WORKER_ID</c> environment variable.</summary>
    EnvironmentVariable,
    /// <summary>Derived from a hash of the machine name (fallback).</summary>
    MachineNameHash,
}

/// <summary>Resolves a 10-bit Snowflake worker id when none is configured explicitly.</summary>
public static class WorkerIdResolver
{
    /// <summary>Maximum worker id value (10 bits).</summary>
    public const int MaxWorkerId = 1023;

    private const string EnvVarName = "ORIONKEY_WORKER_ID";

    /// <summary>Resolves a worker id from the environment variable, else a machine-name hash.</summary>
    public static (int WorkerId, WorkerIdSource Source) Resolve()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVarName);
        if (int.TryParse(raw, out var parsed) && parsed is >= 0 and <= MaxWorkerId)
        {
            return (parsed, WorkerIdSource.EnvironmentVariable);
        }

        var hash = MachineNameHash(Environment.MachineName);
        return (hash & MaxWorkerId, WorkerIdSource.MachineNameHash);
    }

    private static int MachineNameHash(string machineName)
    {
        // FNV-1a 32-bit — stable across processes and runtimes (string.GetHashCode is randomized).
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var c in machineName)
        {
            hash = (hash ^ c) * prime;
        }
        return (int)(hash & int.MaxValue);
    }
}
