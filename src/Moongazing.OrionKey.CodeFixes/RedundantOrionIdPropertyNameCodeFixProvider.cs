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
/// Code fix provider for ORIONKEY012 ("OrionId property name redundantly repeats the
/// id type"). Renames the property to the analyzer-suggested form: `Id` for the entity
/// own id, the navigation-target name for foreign keys.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantOrionIdPropertyNameCodeFixProvider))]
[Shared]
public sealed class RedundantOrionIdPropertyNameCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY012");

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
        var anchor = root.FindNode(diagnostic.Location.SourceSpan);
        var property = anchor.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (property is null)
        {
            return;
        }

        // Parse the suggested name out of the diagnostic message. The descriptor format
        // is: "...; consider renaming to '<Suggested>' to keep entity APIs clean...". We
        // extract the text between the FIRST pair of single-quotes that AREN'T the
        // type/property quotes earlier in the message. The reliable form is to use the
        // diagnostic's MessageArgs index 3 (suggested) but those are not exposed - fall
        // back to a deterministic parse instead.
        var suggested = ExtractSuggested(diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(suggested))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename '{property.Identifier.ValueText}' to '{suggested}'",
                createChangedDocument: ct => RenameAsync(context.Document, property, suggested, ct),
                equivalenceKey: $"ORIONKEY012-rename-{suggested}"),
            diagnostic);
    }

    private static string ExtractSuggested(string message)
    {
        // The message contains: "...Rename to '<Suggested>' to keep..." with the
        // suggested name as the LAST single-quoted segment. Walk from the end to find it.
        var lastClose = message.LastIndexOf('\'');
        if (lastClose < 0)
        {
            return string.Empty;
        }
        var lastOpen = message.LastIndexOf('\'', lastClose - 1);
        if (lastOpen < 0 || lastOpen + 1 >= lastClose)
        {
            return string.Empty;
        }
        return message.Substring(lastOpen + 1, lastClose - lastOpen - 1);
    }

    private static async Task<Document> RenameAsync(
        Document document,
        PropertyDeclarationSyntax property,
        string newName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }
        // Rename ONLY the property declaration's identifier. A semantic rename (Symbol
        // rename API) would cascade to call sites but the CodeFix harness restricts us to
        // syntactic changes within the document. Consumers running the fix from VS get
        // the proper rename refactoring via the IDE's built-in rename command - the fix
        // here is the safe-by-default declaration-only rename.
        var newProperty = property.WithIdentifier(
            SyntaxFactory.Identifier(newName).WithTriviaFrom(property.Identifier));
        return document.WithSyntaxRoot(root.ReplaceNode(property, newProperty));
    }
}
