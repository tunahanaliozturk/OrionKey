namespace Moongazing.OrionKey.Generators.Tests;

using System.Collections.Immutable;
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

public sealed class MemberCollisionCodeFixProviderTests
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

        var root = await trees[0].GetRootAsync();
        var structDecl = root.DescendantNodes().OfType<StructDeclarationSyntax>().First();
        var location = Location.Create(trees[0], structDecl.Identifier.Span);

        var descriptor = new DiagnosticDescriptor("ORIONKEY005",
            "OrionId struct declares a generated member",
            "test", "OrionKey", DiagnosticSeverity.Warning, isEnabledByDefault: true);
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

        var provider = new MemberCollisionCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);

        var operations = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)operations.Single()).ChangedSolution
            .GetDocument(document.Id)!;
        var text = await changed.GetTextAsync();
        return text.ToString();
    }

    [Fact]
    public async Task Removes_user_declared_Value_property()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>]
            public readonly partial struct UserId
            {
                public long Value { get; }
            }
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY005-Value");

        Assert.DoesNotContain("public long Value", fixedSource, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct UserId", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removes_user_declared_ToString_method()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>]
            public readonly partial struct UserId
            {
                public override string ToString() => "x";
            }
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY005-ToString");

        Assert.DoesNotContain("public override string ToString", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removes_user_declared_New_method()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>]
            public readonly partial struct UserId
            {
                public static UserId New() => default;
            }
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY005-New");

        Assert.DoesNotContain("public static UserId New", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removes_user_declared_CompareTo_method()
    {
        const string source = """
            namespace Demo;

            [OrionId<long, Sequential>]
            public readonly partial struct UserId
            {
                public int CompareTo(UserId other) => 0;
            }
            """;

        var fixedSource = await ApplyFixAsync(source, "ORIONKEY005-CompareTo");

        Assert.DoesNotContain("public int CompareTo", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Leaves_non_colliding_members_alone()
    {
        const string source = """
            namespace Demo;

            [OrionId<long>]
            public readonly partial struct UserId
            {
                public long Value { get; }
                public string Description { get; }
            }
            """;

        // Apply Value removal; Description must remain.
        var fixedSource = await ApplyFixAsync(source, "ORIONKEY005-Value");

        Assert.DoesNotContain("public long Value", fixedSource, System.StringComparison.Ordinal);
        Assert.Contains("Description", fixedSource, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY005_as_fixable()
    {
        var provider = new MemberCollisionCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY005", id);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Provider_supports_BatchFixer_for_FixAll_scenarios()
    {
        var provider = new MemberCollisionCodeFixProvider();
        Assert.NotNull(provider.GetFixAllProvider());
        await Task.CompletedTask;
    }
}
