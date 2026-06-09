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
/// Code fix provider for ORIONKEY003 ("string OrionId requires an explicit strategy"). Offers
/// quick fixes that rewrite <c>[OrionId&lt;string&gt;]</c> into
/// <c>[OrionId&lt;string, TStrategy&gt;]</c> with one of the supported string-id strategies.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringStrategyCodeFixProvider))]
[Shared]
public sealed class StringStrategyCodeFixProvider : CodeFixProvider
{
    private static readonly string[] SupportedStrategies =
    {
        "Cuid2",
        "Ulid",
        "NanoId",
        "Ksuid",
        "ObjectId",
    };

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY003");

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

        // The analyzer may report on the attribute itself, the attribute list, or the
        // enclosing struct declaration depending on which check fired. Walk ancestors first
        // (struct -> attribute list), then descendants (attribute list -> attribute) to find
        // the AttributeSyntax for OrionId.
        var anchor = root.FindNode(diagnostic.Location.SourceSpan);
        var attribute = anchor.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault()
            ?? anchor.DescendantNodesAndSelf().OfType<AttributeSyntax>()
                .FirstOrDefault(a => a.Name is GenericNameSyntax g && g.Identifier.Text == "OrionId");
        if (attribute is null)
        {
            return;
        }

        foreach (var strategy in SupportedStrategies)
        {
            var title = $"Use {strategy} string strategy";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => ApplyStrategyAsync(context.Document, attribute, strategy, ct),
                    equivalenceKey: $"ORIONKEY003-{strategy}"),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyStrategyAsync(
        Document document, AttributeSyntax attribute, string strategy, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Replace [OrionId<string>] with [OrionId<string, TStrategy>] by adding a second
        // type argument. The attribute name shape is GenericName -> TypeArgumentList.
        if (attribute.Name is not GenericNameSyntax generic)
        {
            return document;
        }

        var typeArgs = generic.TypeArgumentList;
        var stringArg = typeArgs.Arguments.FirstOrDefault();
        if (stringArg is null)
        {
            return document;
        }

        var strategyType = SyntaxFactory.ParseTypeName(strategy);
        var newTypeArgs = SyntaxFactory.TypeArgumentList(
            SyntaxFactory.SeparatedList(new[] { stringArg, strategyType }));
        var newGeneric = generic.WithTypeArgumentList(newTypeArgs);
        var newAttribute = attribute.WithName(newGeneric);

        var newRoot = root.ReplaceNode(attribute, newAttribute);
        return document.WithSyntaxRoot(newRoot);
    }
}
