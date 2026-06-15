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
    // v0.5.29: Parse and TryParse added so the code fix (not just the ORIONKEY005 detection)
    // can resolve a collision with the now-public parse surface. This allow-list is the
    // provider's own guard against acting on a member name it does not recognise, so it must
    // stay in sync with the names OrionIdParser.CheckMemberCollisions flags.
    private static readonly ImmutableHashSet<string> GeneratedMemberNames = ImmutableHashSet.Create(
        "Value", "New", "Empty", "Equals", "GetHashCode", "ToString", "CompareTo", "Parse", "TryParse");

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

        // The diagnostic reports on the STRUCT location, not the colliding member. The
        // analyzer fires once per colliding member name and stamps the member name into the
        // message format ('{0}' declares a member named '{1}' that the generator also emits).
        // Extract the name from the message so each diagnostic offers a fix for ONLY the
        // member it actually flagged - the analyzer already proved that member would
        // collide with the generator's output, so we don't have to re-derive it here.
        var collidingName = ExtractMemberNameFromMessage(diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        if (collidingName is null || !GeneratedMemberNames.Contains(collidingName))
        {
            return;
        }

        var structDecl = root.FindNode(diagnostic.Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<StructDeclarationSyntax>()
            .FirstOrDefault();
        if (structDecl is null)
        {
            return;
        }

        foreach (var (member, declarator) in CollectCollidingDeclarators(structDecl, collidingName))
        {
            var title = $"Remove user-declared '{collidingName}' (generator emits it)";
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => RemoveMemberOrDeclaratorAsync(context.Document, member, declarator, ct),
                    equivalenceKey: $"ORIONKEY005-{collidingName}"),
                diagnostic);
        }
    }

    // The analyzer message format is:
    //   "'{0}' declares a member named '{1}' that the OrionId generator also emits"
    // Pull the second single-quoted token.
    private static string? ExtractMemberNameFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }
        var first = message.IndexOf('\'');
        if (first < 0)
        {
            return null;
        }
        var firstClose = message.IndexOf('\'', first + 1);
        if (firstClose < 0)
        {
            return null;
        }
        var second = message.IndexOf('\'', firstClose + 1);
        if (second < 0)
        {
            return null;
        }
        var secondClose = message.IndexOf('\'', second + 1);
        if (secondClose < 0)
        {
            return null;
        }
        return message.Substring(second + 1, secondClose - second - 1);
    }

    // Walk the struct's members and yield (member, declarator?) pairs for each declaration
    // whose name matches `name`. For multi-variable field declarations the declarator narrows
    // removal to only the colliding identifier so siblings ('Empty, Sentinel = default') are
    // not silently dropped.
    private static IEnumerable<(MemberDeclarationSyntax Member, VariableDeclaratorSyntax? Declarator)>
        CollectCollidingDeclarators(StructDeclarationSyntax structDecl, string name)
    {
        foreach (var member in structDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax m when m.Identifier.Text == name:
                    yield return (m, null);
                    break;
                case PropertyDeclarationSyntax p when p.Identifier.Text == name:
                    yield return (p, null);
                    break;
                case FieldDeclarationSyntax f:
                    foreach (var decl in f.Declaration.Variables)
                    {
                        if (decl.Identifier.Text == name)
                        {
                            yield return (f, decl);
                        }
                    }
                    break;
            }
        }
    }

    private static async Task<Document> RemoveMemberOrDeclaratorAsync(
        Document document,
        MemberDeclarationSyntax member,
        VariableDeclaratorSyntax? declarator,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        SyntaxNode? newRoot;
        if (declarator is not null && member is FieldDeclarationSyntax field && field.Declaration.Variables.Count > 1)
        {
            // Multi-variable field: drop only the colliding declarator so siblings survive.
            newRoot = root.RemoveNode(declarator, SyntaxRemoveOptions.KeepNoTrivia);
        }
        else
        {
            // Single-variable field, property, or method: drop the whole declaration.
            newRoot = root.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);
        }
        return newRoot is null ? document : document.WithSyntaxRoot(newRoot);
    }
}
