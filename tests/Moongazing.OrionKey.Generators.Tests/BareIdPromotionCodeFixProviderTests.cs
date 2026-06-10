namespace Moongazing.OrionKey.Generators.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Moongazing.OrionKey.CodeFixes;
using Xunit;

public sealed class BareIdPromotionCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source, string propertyName)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Demo.cs");
        var root = await tree.GetRootAsync();
        var prop = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .First(p => p.Identifier.ValueText == propertyName);

        var descriptor = new DiagnosticDescriptor("ORIONKEY008",
            "Bare Guid/long property could be promoted",
            "Property '{0}.{1}' has CLR type '{2}'",
            "OrionKey", DiagnosticSeverity.Info, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor,
            Location.Create(tree, prop.Identifier.Span), "Class", propertyName, "Guid");

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Default,
            "T", "T", LanguageNames.CSharp));
        var document = workspace.AddDocument(project.Id, "Demo.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { selected = action; },
            System.Threading.CancellationToken.None);
        await new BareIdPromotionCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    [Fact]
    public async Task Promotes_bare_Guid_Id_to_class_named_id_struct()
    {
        const string source = """
            namespace Demo;

            public class Order
            {
                public Guid Id { get; set; }
            }
            """;

        var result = await ApplyFixAsync(source, "Id");

        Assert.Contains("public OrderId Id", result, System.StringComparison.Ordinal);
        Assert.Contains("[OrionId<Guid>]", result, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct OrderId;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_CustomerId_property_to_CustomerId_struct()
    {
        const string source = """
            namespace Demo;

            public class Order
            {
                public long CustomerId { get; set; }
            }
            """;

        var result = await ApplyFixAsync(source, "CustomerId");

        Assert.Contains("public CustomerId CustomerId", result, System.StringComparison.Ordinal);
        Assert.Contains("[OrionId<long>]", result, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct CustomerId;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_string_property_with_Ulid_strategy()
    {
        const string source = """
            namespace Demo;

            public class Sku
            {
                public string SkuId { get; set; } = string.Empty;
            }
            """;

        var result = await ApplyFixAsync(source, "SkuId");

        Assert.Contains("[OrionId<string, Ulid>]", result, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct SkuId;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Does_not_re_emit_id_struct_when_one_already_exists_in_file()
    {
        const string source = """
            namespace Demo;

            [OrionId<Guid>]
            public readonly partial struct OrderId;

            public class Order
            {
                public Guid Id { get; set; }
            }
            """;

        var result = await ApplyFixAsync(source, "Id");

        Assert.Contains("public OrderId Id", result, System.StringComparison.Ordinal);
        // The struct should appear EXACTLY once (the pre-existing declaration).
        var occurrences = result.Split("public readonly partial struct OrderId").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY008_as_fixable()
    {
        var provider = new BareIdPromotionCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY008", id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Provider_supports_BatchFixer_for_FixAll_scenarios()
    {
        var provider = new BareIdPromotionCodeFixProvider();
        Assert.NotNull(provider.GetFixAllProvider());
        await Task.CompletedTask;
    }
}
