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

public sealed class BareIdMethodParameterCodeFixProviderTests
{
    private static async Task<string> ApplyFixAsync(string source, string equivalenceKey)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "User.cs");
        var root = await tree.GetRootAsync();
        var parameter = root.DescendantNodes().OfType<ParameterSyntax>().First();
        var descriptor = new DiagnosticDescriptor("ORIONKEY010",
            "Bare id parameter", "Parameter '{0}.{1}'",
            "OrionKey", DiagnosticSeverity.Info, isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor,
            Location.Create(tree, parameter.Identifier.Span), "Demo.Service.GetUser", parameter.Identifier.ValueText);

        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(), VersionStamp.Default,
            "T", "T", LanguageNames.CSharp));
        var document = workspace.AddDocument(project.Id, "User.cs", SourceText.From(source));

        CodeAction? selected = null;
        var context = new CodeFixContext(document, diagnostic,
            (action, _) => { if (action.EquivalenceKey == equivalenceKey) { selected = action; } },
            System.Threading.CancellationToken.None);
        await new BareIdMethodParameterCodeFixProvider().RegisterCodeFixesAsync(context);

        Assert.NotNull(selected);
        var ops = await selected!.GetOperationsAsync(System.Threading.CancellationToken.None);
        var changed = ((ApplyChangesOperation)ops.Single()).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    [Fact]
    public async Task Promotes_Guid_userId_parameter_and_emits_sibling_struct()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public void GetUser(System.Guid userId) { }
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY010-promote-UserId");

        Assert.Contains("GetUser(UserId userId)", result, System.StringComparison.Ordinal);
        Assert.Contains("global::Moongazing.OrionKey.OrionId<Guid>", result, System.StringComparison.Ordinal);
        Assert.Contains("public readonly partial struct UserId;", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_long_orderId_parameter()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public void GetOrder(long orderId) { }
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY010-promote-OrderId");

        Assert.Contains("GetOrder(OrderId orderId)", result, System.StringComparison.Ordinal);
        Assert.Contains("global::Moongazing.OrionKey.OrionId<long>", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Promotes_string_skuId_with_Ulid_strategy()
    {
        const string source = """
            namespace Demo;
            public class Service
            {
                public void GetSku(string skuId) { }
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY010-promote-SkuId");

        Assert.Contains("GetSku(SkuId skuId)", result, System.StringComparison.Ordinal);
        Assert.Contains("global::Moongazing.OrionKey.OrionId<string, global::Moongazing.OrionKey.Ulid>", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reuses_existing_id_struct_in_same_file_without_re_emitting()
    {
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct UserId;
            public class Service
            {
                public void GetUser(System.Guid userId) { }
            }
            """;

        var result = await ApplyFixAsync(source, "ORIONKEY010-promote-UserId");

        Assert.Contains("GetUser(UserId userId)", result, System.StringComparison.Ordinal);
        // Only ONE UserId struct declaration in the result.
        var tree = CSharpSyntaxTree.ParseText(result);
        var root = await tree.GetRootAsync();
        var declarations = root.DescendantNodes().OfType<StructDeclarationSyntax>()
            .Count(s => s.Identifier.ValueText == "UserId");
        Assert.Equal(1, declarations);
    }

    [Fact]
    public async Task Provider_advertises_only_ORIONKEY010_as_fixable()
    {
        var provider = new BareIdMethodParameterCodeFixProvider();
        var id = Assert.Single(provider.FixableDiagnosticIds);
        Assert.Equal("ORIONKEY010", id);
        await Task.CompletedTask;
    }
}
