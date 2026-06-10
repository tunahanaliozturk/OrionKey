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

public sealed class UnusedOrionIdCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source)
    {
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: "User.cs") };
        var references = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location) as MetadataReference)
            .ToList();

        var root = await trees[0].GetRootAsync();
        var structDecl = root.DescendantNodes().OfType<StructDeclarationSyntax>().First();
        var location = Location.Create(trees[0], structDecl.Identifier.Span);

        var descriptor = new DiagnosticDescriptor("ORIONKEY007",
            "OrionId struct is declared but never referenced",
            "'{0}' is declared with [OrionId] but is not referenced anywhere in this compilation",
            "OrionKey", DiagnosticSeverity.Info, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, location, structDecl.Identifier.Text);

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: references);
        var project = workspace.AddProject(projectInfo);
        var document = workspace.AddDocument(project.Id, "User.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { if (action.EquivalenceKey == "ORIONKEY007-remove") { selected = action; } },
            System.Threading.CancellationToken.None);
        var provider = new UnusedOrionIdCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        var text = await changed.GetTextAsync();
        return text.ToString();
    }

    [Fact]
    public async Task Removes_the_unused_struct_declaration()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>]
            public readonly partial struct UnusedId { }

            public class StillHere { }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.DoesNotContain("UnusedId", fixedSource, System.StringComparison.Ordinal);
        Assert.Contains("StillHere", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preserves_surrounding_namespace_after_removal()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>] public readonly partial struct DropMe;

            public class Anchor { public string Name { get; set; } = string.Empty; }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("namespace Demo;", fixedSource, System.StringComparison.Ordinal);
        Assert.Contains("Anchor", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY007_as_fixable()
    {
        var provider = new UnusedOrionIdCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY007", id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Provider_supports_BatchFixer_for_FixAll_scenarios()
    {
        var provider = new UnusedOrionIdCodeFixProvider();
        Assert.NotNull(provider.GetFixAllProvider());
        await Task.CompletedTask;
    }
}
