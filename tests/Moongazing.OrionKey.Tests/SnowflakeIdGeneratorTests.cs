namespace Moongazing.OrionKey.Tests;

public class SnowflakeIdGeneratorTests
{
    private static readonly DateTime Epoch = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Next_ShouldProduceStrictlyIncreasingIds()
    {
        var gen = new SnowflakeIdGenerator(workerId: 1, Epoch);
        long previous = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var id = gen.Next();
            Assert.True(id > previous, $"id {id} not greater than {previous}");
            previous = id;
        }
    }

    [Fact]
    public void Next_ShouldProduceUniqueIds_UnderParallelLoad()
    {
        var gen = new SnowflakeIdGenerator(workerId: 7, Epoch);
        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, 50_000, _ => ids.Add(gen.Next()));
        Assert.Equal(50_000, ids.Distinct().Count());
    }

    [Fact]
    public void Next_ShouldEncodeWorkerIdInBits12To21()
    {
        var gen = new SnowflakeIdGenerator(workerId: 513, Epoch);
        var id = gen.Next();
        var worker = (id >> 12) & 0x3FF;
        Assert.Equal(513, worker);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void Ctor_ShouldThrow_WhenWorkerIdOutOfRange(int workerId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeIdGenerator(workerId, Epoch));
    }

    [Fact]
    public void Next_ShouldThrowClockException_WhenClockMovesBackwards()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var gen = new SnowflakeIdGenerator(workerId: 1, Epoch, clock.Now);
        gen.Next();
        clock.Rewind(TimeSpan.FromSeconds(5));
        Assert.Throws<OrionKeyClockException>(() => gen.Next());
    }

    private sealed class MutableClock(DateTimeOffset start)
    {
        private long ticks = start.UtcTicks;
        public DateTimeOffset Now() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
        public void Rewind(TimeSpan by) => Interlocked.Add(ref ticks, -by.Ticks);
    }
}
