using System.Globalization;
using System.Text.Json;

namespace Moongazing.OrionKey.IntegrationTests;

public class MonotonicHexStrategyTests
{
    [Fact]
    public void New_ProducesThirtyTwoLowercaseHexChars()
    {
        var id = TraceId.New();

        Assert.Equal(32, id.Value.Length);
        Assert.All(id.Value, c => Assert.True(Uri.IsHexDigit(c) && !char.IsUpper(c), $"'{c}' is not a lowercase hex digit"));
        // Round-trips through the BCL hex parser, confirming it is valid hex.
        Assert.Equal(16, Convert.FromHexString(id.Value).Length);
    }

    [Fact]
    public void New_IsUnique_AcrossABatch()
    {
        var ids = TraceId.CreateMany(10_000);
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            Assert.True(distinct.Add(id.Value), $"Duplicate id generated: {id.Value}");
        }
    }

    [Fact]
    public void New_IsMonotonic_WithinAProcess_ByOrdinalComparison()
    {
        // Generated rapidly so most ids share a millisecond timestamp; the randomness-increment
        // path must keep the sequence strictly increasing under ordinal comparison.
        var ids = TraceId.CreateMany(50_000);
        for (var i = 1; i < ids.Length; i++)
        {
            Assert.True(
                string.CompareOrdinal(ids[i - 1].Value, ids[i].Value) < 0,
                $"Not strictly increasing at index {i}: {ids[i - 1].Value} !< {ids[i].Value}");
        }
    }

    [Fact]
    public void CompareTo_PreservesCreationOrder_AcrossMilliseconds()
    {
        var earlier = TraceId.New();
        Thread.Sleep(5);
        var later = TraceId.New();

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
    }

    [Fact]
    public void CompareTo_IsOrdinal_RegardlessOfCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var a = TraceId.New();
            var b = TraceId.New();
            Assert.Equal(
                Math.Sign(string.CompareOrdinal(a.Value, b.Value)),
                Math.Sign(a.CompareTo(b)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void TimestampPrefix_IncreasesOverTime()
    {
        // The first 12 hex chars are the 48-bit millisecond timestamp; a later id must have a
        // timestamp prefix that is greater-or-equal (ordinal), and strictly greater after a sleep.
        var earlier = TraceId.New().Value.Substring(0, 12);
        Thread.Sleep(5);
        var later = TraceId.New().Value.Substring(0, 12);

        Assert.True(string.CompareOrdinal(earlier, later) < 0);
    }

    [Fact]
    public void RoundTrips_ThroughSystemTextJson()
    {
        var original = TraceId.New();
        var restored = JsonSerializer.Deserialize<TraceId>(JsonSerializer.Serialize(original));
        Assert.Equal(original, restored);
    }

    [Fact]
    public void SerializesAsRawString()
    {
        var id = new TraceId("0192f1a0b2c3000000000000deadbeef");
        Assert.Equal("\"0192f1a0b2c3000000000000deadbeef\"", JsonSerializer.Serialize(id));
    }
}
