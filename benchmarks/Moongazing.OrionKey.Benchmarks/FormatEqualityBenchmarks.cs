using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Moongazing.OrionKey.Benchmarks;

/// <summary>
/// Outbound formatting and the equality / comparison surface, the operations EF Core change
/// tracking, dictionary keying, and serialization hit per id on every request. Covers
/// <c>ToString</c>, allocation-free UTF-8 <c>TryFormat</c> (the path System.Text.Json takes),
/// <c>Equals</c>, <c>GetHashCode</c>, and <c>CompareTo</c>. The UTF-8 formatting baseline is
/// <c>ToString</c> + UTF-8 encode, so the ratio shows what the direct-to-bytes path saves versus
/// going through an intermediate string.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class FormatEqualityBenchmarks
{
    private OrderId guidId;
    private OrderId guidIdSame;
    private OrderId guidIdOther;
    private UserId snowflakeId;
    private TenantId ulidId;
    private byte[] utf8Buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        OrionKeyBootstrap.EnsureConfigured();
        guidId = OrderId.New();
        guidIdSame = new OrderId(guidId.Value);
        guidIdOther = OrderId.New();
        snowflakeId = UserId.New();
        ulidId = TenantId.New();
        utf8Buffer = new byte[64];
    }

    // Formatting.
    [Benchmark]
    public string GuidToString() => guidId.ToString();

    [Benchmark]
    public string UlidToString() => ulidId.ToString();

    // UTF-8 formatting: baseline allocates a string then encodes; TryFormat writes bytes directly.
    [Benchmark(Baseline = true)]
    public int GuidToStringThenUtf8()
        => System.Text.Encoding.UTF8.GetBytes(guidId.ToString(), utf8Buffer);

    [Benchmark]
    public int GuidTryFormatUtf8()
    {
        guidId.TryFormat(utf8Buffer, out var bytesWritten, default, null);
        return bytesWritten;
    }

    // Equality / hashing / comparison.
    [Benchmark]
    public bool GuidEquals() => guidId.Equals(guidIdSame);

    [Benchmark]
    public int GuidGetHashCode() => guidId.GetHashCode();

    [Benchmark]
    public int GuidCompareTo() => guidId.CompareTo(guidIdOther);

    [Benchmark]
    public int SnowflakeCompareTo() => snowflakeId.CompareTo(UserId.New());
}
