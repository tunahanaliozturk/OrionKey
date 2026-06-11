namespace Moongazing.OrionKey.Generators.Tests;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Moongazing.OrionKey.Generators.Diagnostics;
using Xunit;

public class BareIdMethodParameterAnalyzerTests
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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new BareIdMethodParameterAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        var diags = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        return diags.Where(d => d.Id == "ORIONKEY010").ToList();
    }

    [Fact]
    public void Flags_Guid_parameter_named_userId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void GetUser(System.Guid userId) { }
            }
            """;

        var diagnostics = Analyze(source);

        Assert.Single(diagnostics);
        Assert.Contains("userId", diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Flags_long_parameter_named_orderId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void GetOrder(long orderId) { }
            }
            """;

        var diagnostics = Analyze(source);
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Flags_string_parameter_named_skuId()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void GetSku(string skuId) { }
            }
            """;

        var diagnostics = Analyze(source);
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Does_not_flag_non_Id_named_parameter()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void Echo(System.Guid token) { }
            }
            """;

        var diagnostics = Analyze(source);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Does_not_flag_already_promoted_OrionId_parameter()
    {
        var source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Service
            {
                public void GetUser(UserId userId) { }
            }
            """;

        var diagnostics = Analyze(source);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Flags_nullable_value_type_id_parameter()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void GetUser(System.Guid? userId) { }
            }
            """;

        var diagnostics = Analyze(source);

        // Nullable<T> wrappers should be unwrapped so the analyzer sees the underlying
        // Guid and fires - matches the v0.5.9 ORIONKEY008 nullable-handling.
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Flags_every_bare_id_parameter_in_a_signature()
    {
        var source = """
            namespace Demo;
            public class Service
            {
                public void Move(System.Guid userId, long orderId) { }
            }
            """;

        var diagnostics = Analyze(source);
        Assert.Equal(2, diagnostics.Count);
    }
}
