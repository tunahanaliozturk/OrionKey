using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

/// <summary>
/// Pins the process-wide Snowflake worker id exactly once. OrionKey.Configure is process-global
/// and throws if called twice, so every [GlobalSetup] routes through this idempotent guard rather
/// than calling Configure directly. A fixed worker id also keeps Snowflake generation deterministic
/// across benchmark runs (no machine-name-hash auto-resolution warning on the hot path).
/// </summary>
internal static class OrionKeyBootstrap
{
    private static readonly object Gate = new();
    private static bool done;

    public static void EnsureConfigured()
    {
        lock (Gate)
        {
            if (done)
            {
                return;
            }

            try
            {
                OrionKey.Configure(o => o.SnowflakeWorkerId = 1);
            }
            catch (InvalidOperationException)
            {
                // Already configured elsewhere in this process; the existing config is fine.
            }

            done = true;
        }
    }
}
