using System.Linq;

namespace Moongazing.OrionKey.Generators.Tests;

public class DiagnosticsTests
{
    private static string[] DiagnosticIds(string source)
        => GeneratorHarness.Run(source).Diagnostics.Select(d => d.Id).ToArray();

    [Fact]
    public void ORIONKEY001_ShouldFire_WhenTargetIsNotReadonlyPartialStruct()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY001", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY002_ShouldFire_WhenValueTypeUnsupported()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<double>] public readonly partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY002", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY003_ShouldFire_WhenStringHasNoStrategy()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string>] public readonly partial struct TenantId;
            """;
        Assert.Contains("ORIONKEY003", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY004_ShouldFire_WhenStrategyIncompatibleWithValueType()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid, Snowflake>] public readonly partial struct OrderId;
            """;
        Assert.Contains("ORIONKEY004", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY005_ShouldWarn_WhenStructDeclaresAGeneratedMember()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>]
            public readonly partial struct OrderId
            {
                public int Value => 0;
            }
            """;
        Assert.Contains("ORIONKEY005", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY005_ShouldWarn_WhenStructDeclaresOwnParse()
    {
        // v0.5.29: the public throwing Parse is now generated, so a consumer-declared Parse
        // must surface as the ORIONKEY005 collision (with its code fix) rather than a raw
        // duplicate-member compile error.
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>]
            public readonly partial struct OrderId
            {
                public static OrderId Parse(string s) => default;
            }
            """;
        Assert.Contains("ORIONKEY005", DiagnosticIds(source));
    }

    [Fact]
    public void ORIONKEY005_ShouldWarn_WhenStructDeclaresOwnTryParse()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>]
            public readonly partial struct OrderId
            {
                public static bool TryParse(string? s, out OrderId result) { result = default; return false; }
            }
            """;
        Assert.Contains("ORIONKEY005", DiagnosticIds(source));
    }

    [Fact]
    public void NoDiagnostics_ForValidDeclaration()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct UserId;
            """;
        Assert.Empty(DiagnosticIds(source));
    }
}
