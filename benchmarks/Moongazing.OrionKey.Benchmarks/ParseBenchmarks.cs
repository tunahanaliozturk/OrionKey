using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

/// <summary>
/// Parse / TryParse on the inbound hot path (route binding, deserialization). Covers the throwing
/// <c>Parse</c>, the non-throwing <c>TryParse</c>, and the allocation-free <c>ReadOnlySpan&lt;char&gt;</c>
/// overload that lets HTTP routing bind without first materializing a string. The Guid baseline is a
/// hand-written "naive parse" (<c>new OrderId(Guid.Parse(text))</c>) so the ratio shows whether the
/// generated Parse adds any measurable overhead over the primitive parse it wraps.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class ParseBenchmarks
{
    private string guidText = null!;
    private string snowflakeText = null!;
    private string ulidText = null!;

    [GlobalSetup]
    public void Setup()
    {
        OrionKeyBootstrap.EnsureConfigured();
        guidText = OrderId.New().ToString();
        snowflakeText = UserId.New().ToString();
        ulidText = TenantId.New().ToString();
    }

    // Guid backing: naive baseline vs generated Parse vs span Parse vs TryParse.
    [Benchmark(Baseline = true)]
    public OrderId NaiveGuidParse() => new(Guid.Parse(guidText));

    [Benchmark]
    public OrderId GuidParse() => OrderId.Parse(guidText);

    [Benchmark]
    public OrderId GuidParseSpan() => OrderId.Parse(guidText.AsSpan());

    [Benchmark]
    public OrderId GuidTryParse()
    {
        OrderId.TryParse(guidText, out var id);
        return id;
    }

    // Snowflake (long) backing.
    [Benchmark]
    public UserId SnowflakeParse() => UserId.Parse(snowflakeText);

    [Benchmark]
    public UserId SnowflakeTryParseSpan()
    {
        UserId.TryParse(snowflakeText.AsSpan(), out var id);
        return id;
    }

    // ULID (string) backing.
    [Benchmark]
    public TenantId UlidParse() => TenantId.Parse(ulidText);
}
