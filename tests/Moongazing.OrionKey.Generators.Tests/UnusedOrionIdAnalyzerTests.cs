using System.Linq;
using System.Threading.Tasks;
using Moongazing.OrionKey.Generators.Diagnostics;

namespace Moongazing.OrionKey.Generators.Tests;

public class UnusedOrionIdAnalyzerTests
{
    [Fact]
    public async Task ORIONKEY007_Fires_WhenStructIsDeclaredAndUnused()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrphanId;
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY007").ToArray();
        Assert.Single(hits);
        Assert.Contains("OrphanId", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ORIONKEY007_DoesNotFire_WhenStructIsUsedAsPropertyType()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;

            public class Holder
            {
                public UsedId Id { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY007", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY007_DoesNotFire_WhenStructIsUsedInAnotherFile()
    {
        const string declaration = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;
            """;

        const string consumer = """
            using Demo;
            namespace Demo.Services;

            public class Service
            {
                public UsedId GetId() => default;
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), declaration, consumer);
        Assert.DoesNotContain("ORIONKEY007", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY007_DoesNotFire_WhenStructIsUsedViaMemberAccess()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;

            public static class Factory
            {
                public static object NewOne() => UsedId.New();
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY007", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY007_DoesNotFire_WhenStructIsUsedAsGenericTypeArgument()
    {
        const string source = """
            using Moongazing.OrionKey;
            using System.Collections.Generic;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;

            public class Holder
            {
                public List<UsedId> Ids { get; } = new();
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY007", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY007_DoesNotFire_WhenStructIsUsedInTypeofOrAttribute()
    {
        const string source = """
            using Moongazing.OrionKey;
            using System;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;

            public class Holder
            {
                public Type Marker { get; } = typeof(UsedId);
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY007", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY007_ReportsOnlyTheUnusedStruct_WhenMixedWithUsedOne()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;
            [OrionId<System.Guid>] public readonly partial struct OrphanId;

            public class Holder
            {
                public UsedId Id { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new UnusedOrionIdAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY007").ToArray();
        Assert.Single(hits);
        Assert.Contains("OrphanId", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }
}
