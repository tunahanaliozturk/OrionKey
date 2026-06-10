using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Moongazing.OrionKey.CodeFixes;

/// <summary>
/// Code fix provider for ORIONKEY007 ("OrionId struct is declared but never referenced").
/// Offers a single quick fix that removes the unused declaration so the consumer's
/// codebase does not carry dead strongly-typed ids.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnusedOrionIdCodeFixProvider))]
[Shared]
public sealed class UnusedOrionIdCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY007");

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.FirstOrDefault();
        if (diagnostic is null)
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        // The diagnostic reports on the struct's identifier; walk ancestors to find the
        // StructDeclarationSyntax we will remove.
        var anchor = root.FindNode(diagnostic.Location.SourceSpan);
        var structDecl = anchor.AncestorsAndSelf().OfType<StructDeclarationSyntax>().FirstOrDefault();
        if (structDecl is null)
        {
            return;
        }

        var structName = structDecl.Identifier.Text;
        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Remove unused OrionId struct '{structName}'",
                createChangedDocument: ct => RemoveStructAsync(context.Document, structDecl, ct),
                equivalenceKey: "ORIONKEY007-remove"),
            diagnostic);
    }

    private static async Task<Document> RemoveStructAsync(
        Document document, StructDeclarationSyntax structDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // KeepEndOfLine keeps the trailing line break so the surrounding namespace/file
        // formatting stays sane. KeepExteriorTrivia preserves leading comments (e.g. a
        // file-level header) that the struct's leading trivia would otherwise carry off.
        var newRoot = root.RemoveNode(
            structDecl,
            SyntaxRemoveOptions.KeepEndOfLine | SyntaxRemoveOptions.KeepExteriorTrivia);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }
}
