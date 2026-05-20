using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Moongazing.OrionKey.Diagnostics;

/// <summary>OrionKey runtime diagnostics: a one-time worker-id warning and an opt-in counter.</summary>
public static class OrionKeyDiagnostics
{
    /// <summary>The OrionKey meter name.</summary>
    public const string MeterName = "Moongazing.OrionKey";

    private static readonly Meter Meter = new(MeterName, "0.1.0");
    private static readonly Counter<long> IdsGenerated =
        Meter.CreateCounter<long>("orionkey.ids.generated");

    private static int workerIdWarningWritten;

    /// <summary>Records one generated id against the meter (no-op unless metrics are enabled).</summary>
    public static void RecordGenerated(string strategy, bool metricsEnabled)
    {
        if (metricsEnabled)
        {
            IdsGenerated.Add(1, new KeyValuePair<string, object?>("strategy", strategy));
        }
    }

    /// <summary>Writes the auto-derived-worker-id warning to the trace listeners exactly once.</summary>
    public static void WarnAutoWorkerIdOnce(int workerId)
    {
        if (Interlocked.Exchange(ref workerIdWarningWritten, 1) == 0)
        {
            Trace.TraceWarning(
                $"OrionKey: Snowflake worker id {workerId} was auto-derived from the machine name. " +
                "In a multi-instance deployment, set ORIONKEY_WORKER_ID or call OrionKey.Configure " +
                "to assign a unique id per instance and avoid id collisions.");
        }
    }
}
