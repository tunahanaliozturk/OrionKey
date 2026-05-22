using System.Globalization;

namespace Moongazing.OrionKey.IntegrationTests;

public class SortableComparisonTests
{
    [Fact]
    public void GuidV7_CompareTo_PreservesCreationOrder()
    {
        var earlier = AuditId.New();
        Thread.Sleep(5);
        var later = AuditId.New();
        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
    }

    [Fact]
    public void SequentialGuid_CompareTo_PreservesCreationOrder()
    {
        var earlier = InvoiceId.New();
        Thread.Sleep(5);
        var later = InvoiceId.New();
        Assert.True(earlier.CompareTo(later) < 0);
    }

    [Fact]
    public void Ulid_CompareTo_IsOrdinal_RegardlessOfCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Under tr-TR, culture-sensitive comparison of 'I'/'i' diverges from ordinal.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var upper = new TenantId("01HZY00000000000000000000I"); // 26 chars
            var lower = new TenantId("01HZY00000000000000000000i"); // 26 chars
            Assert.NotEqual(0, upper.CompareTo(lower));
            Assert.Equal(
                Math.Sign(string.CompareOrdinal(upper.Value, lower.Value)),
                Math.Sign(upper.CompareTo(lower)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Ksuid_CompareTo_OrdersByCreationTime()
    {
        var first = EventId.New();
        Thread.Sleep(1100);
        var second = EventId.New();
        Assert.True(first.CompareTo(second) < 0);
    }
}
