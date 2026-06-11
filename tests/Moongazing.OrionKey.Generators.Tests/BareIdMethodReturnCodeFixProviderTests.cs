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

public sealed class BareIdMethodReturnCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source, string equivalenceKey)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "User.cs");
        var root = await tree.GetRootAsync();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var descriptor = new DiagnosticDescriptor("ORIONKEY011",
            "Bare id return", "Method '{0}.{1}'",
            "OrionKey", DiagnosticSeverity.Info, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor,
            Location.Create(tree, method.Identifier.Span), "Demo.Service", method.Identifier.ValueText);

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Default,
            "T", "T", LanguageNames.CSharp));
        var document = workspace.AddDocument(project.Id, "User.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { if (action.EquivalenceKey == equivalenceKey) { selected = action; } },
            System.Threading.CancellationToken.None);
        await new BareIdMethodReturnCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    [Fact]
    public async Task Promotes_Guid_CreateUserId_return_to_UserId()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public System.Guid CreateUserId() => default;
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-UserId");

        Assert.Contains("UserId CreateUserId()", result, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct UserId;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_Task_T_async_return_preserving_the_wrapper()
    {
        const string source = """
            namespace Demo;
            using System.Threading.Tasks;
            public class Service
            {
                public Task<System.Guid> CreateUserId() => Task.FromResult(System.Guid.Empty);
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-UserId");

        Assert.Contains("Task<UserId> CreateUserId()", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_nullable_Guid_return_preserving_the_annotation()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public System.Guid? TryGetUserId() => null;
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-UserId");

        Assert.Contains("UserId? TryGetUserId()", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_long_GetOrderId_to_OrderId()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public long GetOrderId() => 0;
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-OrderId");

        Assert.Contains("OrderId GetOrderId()", result, System.StringComparison.Ordinal);
        Assert.Contains("global::Moongazing.OrionKey.OrionId<long>", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_string_GetSkuId_with_Ulid_strategy()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public string GetSkuId() => "";
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-SkuId");

        Assert.Contains("global::Moongazing.OrionKey.OrionId<string, global::Moongazing.OrionKey.Ulid>", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reuses_existing_id_struct_in_same_namespace()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Service
            {
                public System.Guid CreateUserId() => default;
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY011-promote-UserId");

        var tree = CSharpSyntaxTree.ParseText(result);
        var declarations = (await tree.GetRootAsync()).DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Count(s => s.Identifier.ValueText == "UserId");
        Assert.Equal(1, declarations);
    }

    [Fact]
    public void Provider_advertises_only_ORIONKEY011_as_fixable()
    {
        var provider = new BareIdMethodReturnCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY011", id);
    }
}
