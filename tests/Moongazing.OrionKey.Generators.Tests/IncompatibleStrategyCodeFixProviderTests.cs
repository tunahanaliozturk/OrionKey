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

public sealed class IncompatibleStrategyCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "User.cs");
        var root = await tree.GetRootAsync();
        // The diagnostic location is the attribute applied to the OrionId struct.
        var attribute = root.DescendantNodes().OfType<AttributeSyntax>()
            .First(a => a.Name is GenericNameSyntax g && g.Identifier.ValueText == "OrionId");

        var descriptor = new DiagnosticDescriptor("ORIONKEY004",
            "Incompatible OrionId strategy",
            "Strategy '{0}' is not compatible with value type '{1}'",
            "OrionKey", DiagnosticSeverity.Error, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor,
            Location.Create(tree, attribute.Span), "Snowflake", "Guid");

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp));
        var document = workspace.AddDocument(project.Id, "User.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { selected = action; },
            System.Threading.CancellationToken.None);
        var provider = new IncompatibleStrategyCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        var text = await changed.GetTextAsync();
        return text.ToString();
    }

    [Fact]
    public async Task Swaps_Snowflake_to_GuidV7_for_Guid_value_type()
    {
        const string source = """
            namespace Demo;

            [OrionId<Guid, Snowflake>]
            public readonly partial struct UserId { }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("OrionId<Guid, GuidV7>", fixedSource, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Snowflake", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swaps_GuidV7_to_Snowflake_for_long_value_type()
    {
        const string source = """
            namespace Demo;

            [OrionId<long, GuidV7>]
            public readonly partial struct OrderId { }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("OrionId<long, Snowflake>", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swaps_strategy_to_Ulid_for_string_value_type()
    {
        const string source = """
            namespace Demo;

            [OrionId<string, Snowflake>]
            public readonly partial struct SkuId { }
            """;

        var fixedSource = await ApplyFixAsync(source);

        Assert.Contains("OrionId<string, Ulid>", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY004_as_fixable()
    {
        var provider = new IncompatibleStrategyCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY004", id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Provider_supports_BatchFixer_for_FixAll_scenarios()
    {
        var provider = new IncompatibleStrategyCodeFixProvider();
        Assert.NotNull(provider.GetFixAllProvider());
        await Task.CompletedTask;
    }
}
