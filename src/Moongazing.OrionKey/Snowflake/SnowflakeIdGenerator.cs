namespace Moongazing.OrionKey.Snowflake;

/// <summary>
/// Twitter-Snowflake id generator: <c>41-bit ms-timestamp | 10-bit worker | 12-bit sequence</c>.
/// Thread-safe. Ids are strictly increasing per process.
/// </summary>
public sealed class SnowflakeIdGenerator
{
    private const int WorkerBits = 10;
    private const int SequenceBits = 12;
    private const int MaxSequence = (1 << SequenceBits) - 1;   // 4095
    private const int WorkerShift = SequenceBits;
    private const int TimestampShift = SequenceBits + WorkerBits;

    private readonly long workerId;
    private readonly long epochMs;
    private readonly Func<DateTimeOffset> clock;
    private readonly object gate = new();

    private long lastTimestamp = -1;
    private long sequence;

    /// <summary>Creates a generator using the system clock.</summary>
    public SnowflakeIdGenerator(int workerId, DateTime epoch)
        : this(workerId, epoch, static () => DateTimeOffset.UtcNow) { }

    /// <summary>Creates a generator with an injectable clock (for tests).</summary>
    public SnowflakeIdGenerator(int workerId, DateTime epoch, Func<DateTimeOffset> clock)
    {
        if (workerId is < 0 or > WorkerIdResolver.MaxWorkerId)
        {
            throw new ArgumentOutOfRangeException(nameof(workerId), workerId,
                $"Worker id must be between 0 and {WorkerIdResolver.MaxWorkerId}.");
        }
        this.workerId = workerId;
        this.clock = clock;
        epochMs = new DateTimeOffset(epoch.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    /// <summary>Generates the next id.</summary>
    /// <exception cref="OrionKeyClockException">The clock moved backwards.</exception>
    public long Next()
    {
        lock (gate)
        {
            var timestamp = clock().ToUnixTimeMilliseconds();

            if (timestamp < lastTimestamp)
            {
                throw new OrionKeyClockException(
                    $"Clock moved backwards by {lastTimestamp - timestamp} ms. Refusing to generate an id.");
            }

            if (timestamp == lastTimestamp)
            {
                sequence = (sequence + 1) & MaxSequence;
                if (sequence == 0)
                {
                    timestamp = WaitNextMillis(lastTimestamp);
                }
            }
            else
            {
                sequence = 0;
            }

            lastTimestamp = timestamp;

            return ((timestamp - epochMs) << TimestampShift)
                 | (workerId << WorkerShift)
                 | sequence;
        }
    }

    private long WaitNextMillis(long lastTs)
    {
        long ts;
        do
        {
            ts = clock().ToUnixTimeMilliseconds();
        }
        while (ts <= lastTs);
        return ts;
    }
}
