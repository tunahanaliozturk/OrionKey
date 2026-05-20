namespace Moongazing.OrionKey;

/// <summary>
/// Process-wide OrionKey facade. Call <see cref="Configure"/> once at startup to set the Snowflake
/// worker id; otherwise it is derived from the environment. Generated id structs call the
/// <c>New*</c> members; application code rarely calls them directly.
/// </summary>
public static class OrionKey
{
    private static readonly object Gate = new();
    private static OrionKeyOptions options = new();
    private static SnowflakeIdGenerator? snowflake;
    private static bool configured;
    private static SequentialState? deterministic;

    /// <summary>
    /// Sets process-wide OrionKey configuration. Must be called at most once, before the first id
    /// is generated.
    /// </summary>
    /// <exception cref="InvalidOperationException">Configuration was already applied.</exception>
    public static void Configure(Action<OrionKeyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        lock (Gate)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "OrionKey.Configure has already been called. Configuration is process-global " +
                    "and cannot change after the first id is generated.");
            }
            var fresh = new OrionKeyOptions();
            configure(fresh);
            options = fresh;
            configured = true;
            snowflake = null;
        }
    }

    /// <summary>Generates the next Snowflake id.</summary>
    public static long NextSnowflake()
    {
        lock (Gate)
        {
            if (deterministic is not null)
            {
                return ++deterministic.Snowflake;
            }
        }
        var generator = GetSnowflake();
        var id = generator.Next();
        OrionKeyDiagnostics.RecordGenerated("snowflake", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new ULID string.</summary>
    public static string NewUlid()
    {
        lock (Gate)
        {
            if (deterministic is not null)
            {
                return (++deterministic.Ulid).ToString("D26", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        var id = UlidFactory.NewUlid();
        OrionKeyDiagnostics.RecordGenerated("ulid", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new NanoId string.</summary>
    public static string NewNanoId()
    {
        lock (Gate)
        {
            if (deterministic is not null)
            {
                return (++deterministic.NanoId).ToString("D21", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        var id = NanoIdFactory.NewNanoId();
        OrionKeyDiagnostics.RecordGenerated("nanoid", options.EnableMetrics);
        return id;
    }

    /// <summary>Generates a new version-7 GUID.</summary>
    public static Guid NewGuidV7()
    {
        lock (Gate)
        {
            if (deterministic is not null)
            {
                var n = ++deterministic.GuidV7;
                Span<byte> b = stackalloc byte[16];
                System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(b.Slice(8), n);
                return new Guid(b, bigEndian: true);
            }
        }
        var id = GuidV7Factory.NewGuidV7();
        OrionKeyDiagnostics.RecordGenerated("guidv7", options.EnableMetrics);
        return id;
    }

    private static SnowflakeIdGenerator GetSnowflake()
    {
        if (snowflake is not null)
        {
            return snowflake;
        }
        lock (Gate)
        {
            if (snowflake is null)
            {
                int workerId;
                if (options.SnowflakeWorkerId is { } explicitId)
                {
                    workerId = explicitId;
                }
                else
                {
                    var (resolved, source) = WorkerIdResolver.Resolve();
                    workerId = resolved;
                    if (source == WorkerIdSource.MachineNameHash)
                    {
                        OrionKeyDiagnostics.WarnAutoWorkerIdOnce(workerId);
                    }
                }
                snowflake = new SnowflakeIdGenerator(workerId, options.SnowflakeEpoch);
            }
            return snowflake;
        }
    }

    /// <summary>Switches to deterministic 1,2,3,... generators. For OrionKey.Testing only.</summary>
    internal static void UseDeterministicGeneratorsForTesting()
    {
        lock (Gate)
        {
            deterministic = new SequentialState();
        }
    }

    /// <summary>Resets process state. For OrionKey.Testing only; not part of the public contract.</summary>
    internal static void ResetForTesting()
    {
        lock (Gate)
        {
            options = new OrionKeyOptions();
            snowflake = null;
            configured = false;
            deterministic = null;
        }
    }

    private sealed class SequentialState
    {
        public long Snowflake;
        public long Ulid;
        public long NanoId;
        public long GuidV7;
    }
}
