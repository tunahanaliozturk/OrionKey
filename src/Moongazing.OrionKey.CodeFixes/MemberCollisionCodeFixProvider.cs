using System.Collections.Generic;
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
/// Code fix provider for ORIONKEY005 ("OrionId struct declares a generated member"). Offers
/// quick fixes that remove user-declared members the OrionId source generator also emits
/// (Value, New, Empty, Equals, GetHashCode, ToString, CompareTo) so the struct compiles
/// without the duplicate-definition error that follows ORIONKEY005.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberCollisionCodeFixProvider))]
[Shared]
public sealed class MemberCollisionCodeFixProvider : CodeFixProvider
{
    private static readonly ImmutableHashSet<string> GeneratedMemberNames = ImmutableHashSet.Create(
        "Value", "New", "Empty", "Equals", "GetHashCode", "ToString", "CompareTo");

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY005");

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

        // The diagnostic reports on the STRUCT location, not the colliding member. Walk the
        // struct's members and offer one fix per collision so the IDE can fix N collisions
        // one at a time (or via FixAll).
        var structDecl = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<StructDeclarationSyntax>()
            .FirstOrDefault();
        if (structDecl is null)
        {
            return;
        }

        foreach (var collidingMember in CollectCollidingMembers(structDecl))
        {
            var memberName = GetMemberName(collidingMember);
            var title = $"Remove user-declared '{memberName}' (generator emits it)";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => RemoveMemberAsync(context.Document, collidingMember, ct),
                    equivalenceKey: $"ORIONKEY005-{memberName}"),
                diagnostic);
        }
    }

    private static IEnumerable<MemberDeclarationSyntax> CollectCollidingMembers(StructDeclarationSyntax structDecl)
    {
        foreach (var member in structDecl.Members)
        {
            var name = GetMemberName(member);
            if (name is not null && GeneratedMemberNames.Contains(name))
            {
                yield return member;
            }
        }
    }

    private static string? GetMemberName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        PropertyDeclarationSyntax prop => prop.Identifier.Text,
        FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        _ => null,
    };

    private static async Task<Document> RemoveMemberAsync(
        Document document, MemberDeclarationSyntax member, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }
}
