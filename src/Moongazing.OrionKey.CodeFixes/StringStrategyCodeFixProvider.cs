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
        // the OrionId AttributeSyntax. The descendant search inspects the underlying
        // GenericNameSyntax inside any qualified / alias-qualified name shape.
        var anchor = root.FindNode(diagnostic.Location.SourceSpan);
        var attribute = anchor.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault()
            ?? anchor.DescendantNodesAndSelf().OfType<AttributeSyntax>()
                .FirstOrDefault(a => IsOrionIdAttribute(a.Name));
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
        // type argument. The attribute name may appear in any of:
        //   [OrionId<string>]                                 -> GenericNameSyntax
        //   [Moongazing.OrionKey.OrionId<string>]             -> QualifiedNameSyntax
        //   [global::Moongazing.OrionKey.OrionId<string>]     -> AliasQualifiedNameSyntax
        //   [OrionIdAttribute<string>]                        -> GenericNameSyntax with "OrionIdAttribute"
        // Walk the name shape to find the underlying GenericNameSyntax.
        var generic = ExtractGenericName(attribute.Name);
        if (generic is null)
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

        // Splice newGeneric back into the original name shape, preserving any qualifier.
        var newName = RebuildName(attribute.Name, newGeneric);
        var newAttribute = attribute.WithName(newName);

        var newRoot = root.ReplaceNode(attribute, newAttribute);
        return document.WithSyntaxRoot(newRoot);
    }

    private static GenericNameSyntax? ExtractGenericName(NameSyntax name) => name switch
    {
        GenericNameSyntax g => g,
        QualifiedNameSyntax q => ExtractGenericName(q.Right),
        AliasQualifiedNameSyntax a => ExtractGenericName(a.Name),
        _ => null,
    };

    private static bool IsOrionIdAttribute(NameSyntax name)
    {
        var generic = ExtractGenericName(name);
        if (generic is null)
        {
            return false;
        }
        var ident = generic.Identifier.Text;
        return ident == "OrionId" || ident == "OrionIdAttribute";
    }

    private static NameSyntax RebuildName(NameSyntax original, GenericNameSyntax newGeneric) => original switch
    {
        GenericNameSyntax => newGeneric,
        QualifiedNameSyntax q => q.WithRight(newGeneric),
        AliasQualifiedNameSyntax a => a.WithName(newGeneric),
        _ => newGeneric,
    };
}
