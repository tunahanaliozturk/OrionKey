using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

/// <summary>
/// Per-strategy raw id generation via the <see cref="OrionKey"/> facade. This is the floor cost of
/// minting one id of each kind. <see cref="RawGuid"/> is the baseline: it is the cheapest identifier
/// .NET offers, so every ratio answers "what does sortability / smaller storage / a textual format
/// cost relative to a plain Guid?".
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class RawGenerationBenchmarks
{
    [GlobalSetup]
    public void Setup() => OrionKeyBootstrap.EnsureConfigured();

    [Benchmark(Baseline = true)]
    public Guid RawGuid() => Guid.NewGuid();

    [Benchmark]
    public long Snowflake() => OrionKey.NextSnowflake();

    [Benchmark]
    public Guid GuidV7() => OrionKey.NewGuidV7();

    [Benchmark]
    public Guid SequentialGuid() => OrionKey.NewSequentialGuid();

    [Benchmark]
    public string Ulid() => OrionKey.NewUlid();

    [Benchmark]
    public string NanoId() => OrionKey.NewNanoId();

    [Benchmark]
    public string ObjectId() => OrionKey.NewObjectId();

    [Benchmark]
    public string Ksuid() => OrionKey.NewKsuid();

    [Benchmark]
    public string Cuid2() => OrionKey.NewCuid2();
}
