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

public sealed class RedundantOrionIdPropertyNameCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source, string equivalenceKey, string diagnosticMessage)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "User.cs");
        var root = await tree.GetRootAsync();
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
        var descriptor = new DiagnosticDescriptor("ORIONKEY012",
            "Redundant id name", diagnosticMessage,
            "OrionKey", DiagnosticSeverity.Info, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(tree, property.Identifier.Span));

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Default,
            "T", "T", LanguageNames.CSharp));
        var document = workspace.AddDocument(project.Id, "User.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { if (action.EquivalenceKey == equivalenceKey) { selected = action; } },
            System.Threading.CancellationToken.None);
        await new RedundantOrionIdPropertyNameCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    [Fact]
    public async Task Renames_self_referential_OrderId_to_Id()
    {
        const string source = """
            namespace Demo;
            public class Order
            {
                public OrderId OrderId { get; set; }
            }
            """;

        // The diagnostic message is built as 'Rename to 'Id' to keep entity APIs clean...'
        const string message = "Property 'Order.OrderId' is typed as 'OrderId' and named 'OrderId'; the property name redundantly repeats the id type. Rename to 'Id' to keep entity APIs clean";
        var result = await ApplyFixAsync(source, "ORIONKEY012-rename-Id", message);

        Assert.Contains("public OrderId Id { get; set; }", result, System.StringComparison.Ordinal);
        Assert.DoesNotContain("OrderId OrderId", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Renames_foreign_key_UserId_to_User()
    {
        const string source = """
            namespace Demo;
            public class Order
            {
                public UserId UserId { get; set; }
            }
            """;

        const string message = "Property 'Order.UserId' is typed as 'UserId' and named 'UserId'; the property name redundantly repeats the id type. Rename to 'User' to keep entity APIs clean";
        var result = await ApplyFixAsync(source, "ORIONKEY012-rename-User", message);

        Assert.Contains("public UserId User { get; set; }", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_advertises_only_ORIONKEY012_as_fixable()
    {
        var provider = new RedundantOrionIdPropertyNameCodeFixProvider();
        Assert.Equal("ORIONKEY012", Assert.Single(provider.FixableDiagnosticIds));
    }
}
