using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace Moongazing.OrionKey;

/// <summary>OrionKey runtime diagnostics: a one-time worker-id warning and an opt-in counter.</summary>
public static class OrionKeyDiagnostics
{
    /// <summary>The OrionKey meter name.</summary>
    public const string MeterName = "Moongazing.OrionKey";

    private static readonly Meter Meter = new(MeterName, MeterVersion.Value);
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

/// <summary>Derives the diagnostics meter version once from the assembly informational version.</summary>
internal static class MeterVersion
{
    /// <summary>The meter version string, derived from the owning assembly's version.</summary>
    public static string Value { get; } = Resolve();

    private static string Resolve()
    {
        var asm = typeof(MeterVersion).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
