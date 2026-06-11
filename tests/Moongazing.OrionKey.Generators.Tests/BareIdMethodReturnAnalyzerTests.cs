namespace Moongazing.OrionKey.Generators.Tests;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Moongazing.OrionKey.Generators.Diagnostics;
using Xunit;

public class BareIdMethodReturnAnalyzerTests
{
    private static System.Collections.Generic.List<Diagnostic> Analyze(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;
        var references = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new BareIdMethodReturnAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        var diags = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        return diags.Where(d => d.Id == "ORIONKEY011").ToList();
    }

    [Fact]
    public void Flags_method_returning_Guid_named_CreateUserId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public System.Guid CreateUserId() => default;
            }
            """;

        Assert.Single(Analyze(source));
    }

    [Fact]
    public void Flags_method_returning_long_named_GetOrderId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public long GetOrderId() => 0;
            }
            """;

        Assert.Single(Analyze(source));
    }

    [Fact]
    public void Flags_Task_T_async_return_named_NewSku_Id_via_string()
    {
        var source = """
            namespace Demo;
            using System.Threading.Tasks;
            public class Service
            {
                public Task<string> CreateSkuId() => Task.FromResult("x");
            }
            """;

        // CreateSkuId ends with "Id" and Task<string> unwraps to bare string -> fires.
        Assert.Single(Analyze(source));
    }

    [Fact]
    public void Flags_nullable_Guid_return_named_TryGetId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public System.Guid? TryGetId() => null;
            }
            """;

        Assert.Single(Analyze(source));
    }

    [Fact]
    public void Does_not_flag_method_whose_name_does_not_end_with_Id()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public System.Guid Identify() => default;
            }
            """;

        // "Identify" CONTAINS Id but does not end with it - excluded by design.
        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void Does_not_flag_method_returning_already_promoted_OrionId()
    {
        var source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Service
            {
                public UserId CreateUserId() => default;
            }
            """;

        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void Does_not_flag_constructor_or_operator()
    {
        var source = """
            namespace Demo;
            public class FooId
            {
                public FooId() { }
                public static FooId operator +(FooId a, FooId b) => new();
            }
            """;

        Assert.Empty(Analyze(source));
    }
}
