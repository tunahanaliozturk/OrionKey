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
/// Code fix provider for ORIONKEY004 ("Incompatible OrionId strategy"). Offers to swap
/// the incompatible strategy type argument for one that matches the chosen value type:
/// <list type="bullet">
///   <item><description><c>Guid</c> -&gt; <c>GuidV7</c></description></item>
///   <item><description><c>long</c> -&gt; <c>Snowflake</c></description></item>
///   <item><description><c>string</c> -&gt; <c>Ulid</c></description></item>
/// </list>
/// The other compatible strategies (e.g. <c>SequentialGuid</c>, <c>NanoId</c>) are NOT
/// offered to avoid cluttering the fix list - consumers who want them rewrite manually.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IncompatibleStrategyCodeFixProvider))]
[Shared]
public sealed class IncompatibleStrategyCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY004");

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

        // ORIONKEY004 is reported with symbol.Locations[0] - that source span points at
        // the struct's identifier, NOT the attribute. So we first try walking UP (in case
        // a future analyzer revision points at the attribute directly), then fall back to
        // walking DOWN from the enclosing struct to find the OrionId<TValue, TStrategy>
        // attribute. Either path must yield the GenericNameSyntax we are about to rewrite.
        var anchor = root.FindNode(diagnostic.Location.SourceSpan);
        var attribute = anchor.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault();
        if (attribute is null)
        {
            var structDecl = anchor.AncestorsAndSelf().OfType<StructDeclarationSyntax>().FirstOrDefault();
            attribute = structDecl?
                .AttributeLists
                .SelectMany(list => list.Attributes)
                .FirstOrDefault(a => a.Name is GenericNameSyntax g
                                     && g.Identifier.ValueText == "OrionId"
                                     && g.TypeArgumentList.Arguments.Count == 2);
        }
        if (attribute?.Name is not GenericNameSyntax generic)
        {
            return;
        }
        var typeArgs = generic.TypeArgumentList.Arguments;
        if (typeArgs.Count != 2)
        {
            return;
        }
        var valueArg = typeArgs[0];
        var strategyArg = typeArgs[1];

        if (!TryResolveSwap(valueArg, out var recommendedStrategy))
        {
            return;
        }

        var title = $"Change strategy to '{recommendedStrategy}' to match value type";
        context.RegisterCodeFix(
            CodeAction.Create(
                title: title,
                createChangedDocument: ct => SwapStrategyAsync(
                    context.Document, generic, strategyArg, recommendedStrategy, ct),
                equivalenceKey: $"ORIONKEY004-swap-{recommendedStrategy}"),
            diagnostic);
    }

    private static bool TryResolveSwap(TypeSyntax valueArg, out string recommendedStrategy)
    {
        // We compare by syntactic name rather than resolving the symbol; the user's value
        // arg is one of `Guid`, `long`, `Int64`, `int`, `Int32`, or `string`. Aliased
        // BCL types parse to either the keyword form (`long`) or the type-name form
        // (`Int64`). Both are handled.
        recommendedStrategy = valueArg switch
        {
            PredefinedTypeSyntax { Keyword.ValueText: "long" } => "Snowflake",
            PredefinedTypeSyntax { Keyword.ValueText: "string" } => "Ulid",
            IdentifierNameSyntax { Identifier.ValueText: "Guid" } => "GuidV7",
            IdentifierNameSyntax { Identifier.ValueText: "Int64" } => "Snowflake",
            QualifiedNameSyntax q when q.Right.Identifier.ValueText == "Guid" => "GuidV7",
            QualifiedNameSyntax q when q.Right.Identifier.ValueText == "Int64" => "Snowflake",
            _ => string.Empty,
        };
        return !string.IsNullOrEmpty(recommendedStrategy);
    }

    private static async Task<Document> SwapStrategyAsync(
        Document document,
        GenericNameSyntax generic,
        TypeSyntax oldStrategy,
        string recommendedStrategy,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Preserve the leading / trailing trivia of the strategy type-arg so the rewrite
        // does not eat spaces / comments inside the type-argument list.
        var newStrategy = SyntaxFactory.IdentifierName(recommendedStrategy)
            .WithTriviaFrom(oldStrategy);
        var newGeneric = generic.WithTypeArgumentList(
            generic.TypeArgumentList.WithArguments(
                SyntaxFactory.SeparatedList(new[] { generic.TypeArgumentList.Arguments[0], (TypeSyntax)newStrategy })));

        return document.WithSyntaxRoot(root.ReplaceNode(generic, newGeneric));
    }
}
