using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

/// <summary>
/// Measures the overhead the generated strongly-typed wrapper adds over calling the underlying
/// generator directly. The wrapper is a <c>readonly struct</c> holding a single value and its
/// <c>New()</c> just forwards to the strategy, so the expectation is "indistinguishable from raw":
/// these benchmarks exist to PROVE that, not to assume it.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class TypedIdGenerationBenchmarks
{
    private const int BatchSize = 100;

    [GlobalSetup]
    public void Setup() => OrionKeyBootstrap.EnsureConfigured();

    // Guid-backed typed id vs the raw Guid.NewGuid() it wraps.
    [Benchmark(Baseline = true)]
    public Guid RawGuid() => Guid.NewGuid();

    [Benchmark]
    public OrderId TypedGuidId() => OrderId.New();

    // Snowflake-backed typed id vs the raw long generator it wraps.
    [Benchmark]
    public long RawSnowflake() => OrionKey.NextSnowflake();

    [Benchmark]
    public UserId TypedSnowflakeId() => UserId.New();

    // ULID-backed typed id vs the raw string generator it wraps.
    [Benchmark]
    public string RawUlid() => OrionKey.NewUlid();

    [Benchmark]
    public TenantId TypedUlidId() => TenantId.New();

    // Bulk creation via the generated CreateMany helper (pre-allocated array, no LINQ).
    [Benchmark]
    public OrderId[] CreateMany_Guid() => OrderId.CreateMany(BatchSize);

    [Benchmark]
    public UserId[] CreateMany_Snowflake() => UserId.CreateMany(BatchSize);
}
