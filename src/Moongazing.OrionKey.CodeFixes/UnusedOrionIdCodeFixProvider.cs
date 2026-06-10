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
                createChangedSolution: ct => RemoveAllPartialDeclarationsAsync(context.Document, structDecl, ct),
                equivalenceKey: "ORIONKEY007-remove"),
            diagnostic);
    }

    private static async Task<Solution> RemoveAllPartialDeclarationsAsync(
        Document document, StructDeclarationSyntax structDecl, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        // [OrionId] structs are declared `readonly partial struct`; a single type can have
        // multiple partial declarations across files. ORIONKEY007 reports on one Location,
        // so removing JUST the StructDeclarationSyntax under the diagnostic anchor would
        // leave the rest of the partial type behind. Resolve the symbol, walk every
        // DeclaringSyntaxReference, and remove the StructDeclarationSyntax ancestor of
        // each one so the type is removed wholesale.
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var symbol = semanticModel?.GetDeclaredSymbol(structDecl, cancellationToken);
        if (symbol is null)
        {
            // Fall back to the single-document removal if we cannot resolve the symbol.
            return await RemoveSingleAsync(document, structDecl, cancellationToken).ConfigureAwait(false);
        }

        // Group partial declarations by document so we apply one edit per document.
        var byDocument = symbol.DeclaringSyntaxReferences
            .GroupBy(r => solution.GetDocument(r.SyntaxTree));

        foreach (var group in byDocument)
        {
            var doc = group.Key;
            if (doc is null)
            {
                continue;
            }
            var docRoot = await doc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (docRoot is null)
            {
                continue;
            }
            // Remove each StructDeclarationSyntax that contains a referenced syntax node.
            var nodesToRemove = group
                .Select(r => r.GetSyntax(cancellationToken))
                .Select(s => s as StructDeclarationSyntax ?? s.AncestorsAndSelf().OfType<StructDeclarationSyntax>().FirstOrDefault())
                .Where(s => s is not null)
                .Distinct()
                .ToList();
            if (nodesToRemove.Count == 0)
            {
                continue;
            }
            var newRoot = docRoot.RemoveNodes(
                nodesToRemove!,
                SyntaxRemoveOptions.KeepEndOfLine | SyntaxRemoveOptions.KeepExteriorTrivia);
            if (newRoot is null)
            {
                continue;
            }
            solution = solution.WithDocumentSyntaxRoot(doc.Id, newRoot);
        }

        return solution;
    }

    private static async Task<Solution> RemoveSingleAsync(
        Document document, StructDeclarationSyntax structDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document.Project.Solution;
        }
        var newRoot = root.RemoveNode(
            structDecl,
            SyntaxRemoveOptions.KeepEndOfLine | SyntaxRemoveOptions.KeepExteriorTrivia);
        return newRoot is null
            ? document.Project.Solution
            : document.WithSyntaxRoot(newRoot).Project.Solution;
    }
}
