namespace Moongazing.OrionKey.Generators.Tests;

using System.Linq;
using System.Threading.Tasks;
using Moongazing.OrionKey.Generators.Diagnostics;
using Xunit;

public sealed class BareIdPromotionAnalyzerTests
{
    [Fact]
    public async Task Guid_property_named_Id_triggers_ORIONKEY008()
    {
        const string source = """
            using System;
            namespace Demo;

            public class Customer
            {
                public Guid Id { get; set; }
                public string Name { get; set; } = "";
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY008").ToArray();
        Assert.Single(hits);
        Assert.Contains("Customer", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Long_property_ending_in_Id_triggers_ORIONKEY008()
    {
        const string source = """
            namespace Demo;

            public class Order
            {
                public long CustomerId { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY008").ToArray();
        Assert.Single(hits);
        Assert.Contains("CustomerId", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Int_property_ending_in_Id_triggers_ORIONKEY008()
    {
        const string source = """
            namespace Demo;

            public class Audit
            {
                public int WorkflowStepId { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY008").ToArray();
        Assert.Single(hits);
    }

    [Fact]
    public async Task DateTime_property_named_Id_does_not_trigger()
    {
        // DateTime is not a candidate type in v0.5.
        const string source = """
            using System;
            namespace Demo;

            public class Event
            {
                public DateTime OccurredId { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY008", diags.Select(d => d.Id));
    }

    [Fact]
    public async Task Property_that_does_not_end_in_Id_does_not_trigger()
    {
        const string source = """
            using System;
            namespace Demo;

            public class Document
            {
                public Guid CorrelationGuid { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY008", diags.Select(d => d.Id));
    }

    [Fact]
    public async Task Static_Id_property_does_not_trigger()
    {
        const string source = """
            using System;
            namespace Demo;

            public class Singleton
            {
                public static Guid Id { get; } = Guid.Empty;
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY008", diags.Select(d => d.Id));
    }

    [Fact]
    public async Task Property_already_using_OrionId_struct_does_not_trigger()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct CustomerId;

            public class Customer
            {
                public CustomerId Id { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY008", diags.Select(d => d.Id));
    }

    [Fact]
    public async Task Nullable_Guid_property_named_Id_still_triggers()
    {
        const string source = """
            using System;
            namespace Demo;

            public class Optional
            {
                public Guid? Id { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new BareIdPromotionAnalyzer(), source);
        Assert.Single(diags.Where(d => d.Id == "ORIONKEY008"));
    }
}
