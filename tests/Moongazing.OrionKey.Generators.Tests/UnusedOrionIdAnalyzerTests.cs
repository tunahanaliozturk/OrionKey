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

    [Fact]
    public async Task Generator_emitted_partials_do_not_mask_unused()
    {
        // Regression test for the v0.5.0 known limitation noted in CHANGELOG: when the
        // OrionKey source generator runs alongside the analyzer in a real consumer build,
        // its emitted partial declarations of the OrionId struct contain self-references
        // (IEquatable<OrderId>, public static OrderId New(), value converters, JSON
        // converters, etc). v0.5.0's analyzer counted those as references, so ORIONKEY007
        // silently never fired for genuinely unused types. v0.5.1 filters generator-emitted
        // trees (path ending in `.g.cs`) from the reference scan.
        const string userSource = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrphanId;
            """;

        // Faithful mock of the generator-emitted partial: same struct, IEquatable<OrphanId>
        // self-reference, a factory method returning OrphanId. None of these should count
        // as "user references" to OrphanId.
        const string generatedSource = """
            namespace Demo;

            partial struct OrphanId : System.IEquatable<OrphanId>
            {
                public static OrphanId New() => default;
                public bool Equals(OrphanId other) => true;
                public override bool Equals(object? obj) => obj is OrphanId other && Equals(other);
                public override int GetHashCode() => 0;
            }
            """;

        var diags = await AnalyzerHarness.RunWithGeneratedAsync(
            new UnusedOrionIdAnalyzer(),
            userSources: new[] { userSource },
            generatedSources: new[] { generatedSource });

        var hits = diags.Where(d => d.Id == "ORIONKEY007").ToArray();
        Assert.Single(hits);
        Assert.Contains("OrphanId", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Generator_emitted_consumer_in_different_type_still_counts_as_used()
    {
        // Counter-test for the bot's concern on the v0.5.1 first-pass fix: filtering
        // ENTIRE generated trees would drop legitimate references made from generator-
        // emitted source (Mediator handlers, generated DTOs with an OrderId property, etc).
        // The fix uses a self-reference filter instead: only references that appear
        // syntactically INSIDE a partial declaration of the same OrionId struct are
        // dropped; references inside a DIFFERENT type (even if that type is also
        // generator-emitted) still count as usage.
        const string userSource = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct UsedId;
            """;

        // Mock of generator-emitted consumer code: a generated record type with an
        // UsedId property. This should count as a real reference even though it lives
        // in a `.g.cs` file.
        const string generatedConsumerSource = """
            namespace Demo;

            public sealed record CreateOrderCommand(UsedId Id, string Note);
            """;

        var diags = await AnalyzerHarness.RunWithGeneratedAsync(
            new UnusedOrionIdAnalyzer(),
            userSources: new[] { userSource },
            generatedSources: new[] { generatedConsumerSource });

        // UsedId is referenced from a generator-emitted CONSUMER (CreateOrderCommand),
        // so ORIONKEY007 must NOT fire.
        var hits = diags.Where(d => d.Id == "ORIONKEY007").ToArray();
        Assert.Empty(hits);
    }
}
