using BenchmarkDotNet.Attributes;
using Moongazing.OrionKey;

namespace Moongazing.OrionKey.Benchmarks;

[MemoryDiagnoser]
public class IdGenerationBenchmarks
{
    [Benchmark(Baseline = true)]
    public Guid RawGuid() => Guid.NewGuid();

    [Benchmark]
    public long Snowflake() => OrionKey.NextSnowflake();

    [Benchmark]
    public string Ulid() => OrionKey.NewUlid();

    [Benchmark]
    public string NanoId() => OrionKey.NewNanoId();

    [Benchmark]
    public Guid GuidV7() => OrionKey.NewGuidV7();
}
