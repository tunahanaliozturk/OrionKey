namespace Moongazing.OrionKey.Generators.Tests;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Moongazing.OrionKey.Generators.Diagnostics;
using Xunit;

public class RedundantOrionIdPropertyNameAnalyzerTests
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

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new RedundantOrionIdPropertyNameAnalyzer());
        var withAnalyzers = compilation.WithAnalyzers(analyzers);
        var diags = withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        return diags.Where(d => d.Id == "ORIONKEY012").ToList();
    }

    [Fact]
    public void Flags_self_referential_redundant_naming_and_suggests_Id()
    {
        // Order entity with OrderId-typed property named OrderId. Suggestion: rename to Id.
        var source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            public class Order
            {
                public OrderId OrderId { get; set; }
            }
            """;

        var diagnostics = Analyze(source);

        var diag = Assert.Single(diagnostics);
        Assert.Contains("'Id'", diag.GetMessage(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Flags_foreign_key_redundant_naming_and_suggests_unprefixed_form()
    {
        // Order entity referencing a User entity via a UserId-typed property named UserId.
        // Suggestion: rename to User (the navigation target).
        var source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Order
            {
                public UserId UserId { get; set; }
            }
            """;

        var diagnostics = Analyze(source);

        var diag = Assert.Single(diagnostics);
        Assert.Contains("'User'", diag.GetMessage(System.Globalization.CultureInfo.InvariantCulture), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Does_not_flag_property_with_different_name_from_its_OrionId_type()
    {
        var source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Order
            {
                public UserId BuyerId { get; set; }
            }
            """;

        Assert.Empty(Analyze(source));
    }

    [Fact]
    public void Does_not_flag_bare_primitive_property_even_when_name_matches_type()
    {
        // Plain `Guid Guid` would be silly but it is not an OrionId so out of scope.
        var source = """
            namespace Demo;
            public class Order
            {
                public System.Guid Guid { get; set; }
            }
            """;

        Assert.Empty(Analyze(source));
    }
}
