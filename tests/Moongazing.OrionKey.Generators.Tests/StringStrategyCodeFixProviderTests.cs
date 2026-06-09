namespace Moongazing.OrionKey.Generators.Tests;

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Moongazing.OrionKey.CodeFixes;
using Xunit;

public sealed class StringStrategyCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source, string equivalenceKey)
    {
        var trees = new[] { CSharpSyntaxTree.ParseText(source, path: "User.cs") };
        var references = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location) as MetadataReference)
            .ToList();

        var compilation = CSharpCompilation.Create("CodeFixTest", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Fake a diagnostic at the attribute location so the code fix can fire. The test
        // source may use any legal attribute-name shape; match either OrionId<...> or
        // OrionIdAttribute<...>. Find OrionId then walk forward to the '<'.
        var orionPos = source.IndexOf("OrionId", System.StringComparison.Ordinal);
        Assert.True(orionPos >= 0, "test source must contain OrionId");
        var orionIdPos = source.IndexOf('<', orionPos);
        Assert.True(orionIdPos >= 0, "test source must contain '<' after OrionId");
        var attributeStart = source.LastIndexOf('[', orionIdPos);
        Assert.True(attributeStart >= 0, "test source must contain an attribute list around OrionId<...>");
        var openBracket = source.IndexOf(']', attributeStart);
        Assert.True(openBracket > attributeStart);
        var location = Location.Create(trees[0],
            TextSpan.FromBounds(attributeStart, openBracket + 1));

        var descriptor = new DiagnosticDescriptor("ORIONKEY003",
            "string OrionId requires an explicit strategy",
            "test", "OrionKey", DiagnosticSeverity.Error, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, location);

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
            (action, _) =>
            {
                if (action.EquivalenceKey == equivalenceKey)
                {
                    selected = action;
                }
            },
            System.Threading.CancellationToken.None);

        var provider = new StringStrategyCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);

        var operations = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)operations.Single()).ChangedSolution
            .GetDocument(document.Id)!;
        var text = await changed.GetTextAsync();
        return text.ToString();
    }

    [Fact]
    public async Task Fix_inserts_Cuid2_strategy_into_OrionId_attribute()
    {
        const string source = """
            namespace Demo;

            [OrionId<string>] public readonly partial struct UserName;
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY003-Cuid2");

        Assert.Contains("[OrionId<string, Cuid2>]", fixedSource, System.StringComparison.Ordinal);
        Assert.DoesNotContain("[OrionId<string>]", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_inserts_Ulid_strategy_into_OrionId_attribute()
    {
        const string source = """
            namespace Demo;

            [OrionId<string>] public readonly partial struct UserName;
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY003-Ulid");

        Assert.Contains("[OrionId<string, Ulid>]", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_handles_fully_qualified_attribute_name()
    {
        const string source = """
            namespace Demo;

            [Moongazing.OrionKey.OrionId<string>] public readonly partial struct UserName;
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY003-Cuid2");

        Assert.Contains("[Moongazing.OrionKey.OrionId<string, Cuid2>]", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_handles_alias_qualified_attribute_name()
    {
        const string source = """
            namespace Demo;

            [global::Moongazing.OrionKey.OrionId<string>] public readonly partial struct UserName;
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY003-Cuid2");

        Assert.Contains("[global::Moongazing.OrionKey.OrionId<string, Cuid2>]", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fix_handles_OrionIdAttribute_suffix()
    {
        const string source = """
            namespace Demo;

            [OrionIdAttribute<string>] public readonly partial struct UserName;
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY003-Cuid2");

        Assert.Contains("[OrionIdAttribute<string, Cuid2>]", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY003_as_fixable()
    {
        var provider = new StringStrategyCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY003", id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Provider_supports_BatchFixer_for_FixAll_scenarios()
    {
        var provider = new StringStrategyCodeFixProvider();
        Assert.NotNull(provider.GetFixAllProvider());
        await Task.CompletedTask;
    }
}
