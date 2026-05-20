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
