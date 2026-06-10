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
/// Code fix provider for ORIONKEY008 ("Bare Guid/long/string property named Id or *Id
/// could be promoted to a strongly-typed id"). Rewrites the property's type to a derived
/// id name and emits a sibling <c>[OrionId&lt;TValue&gt;] public readonly partial struct</c>
/// in the same compilation unit so the consumer's next build picks up the generated
/// converter automatically.
/// </summary>
/// <remarks>
/// <para>
/// Naming rule for the new id:
/// </para>
/// <list type="bullet">
///   <item><description>Property name is exactly <c>Id</c> -> new id is <c>{ClassName}Id</c> (e.g. <c>Order.Id</c> -> <c>OrderId</c>).</description></item>
///   <item><description>Property name ends with <c>Id</c> -> new id is the property name itself (e.g. <c>CustomerId</c> -> <c>CustomerId</c>).</description></item>
/// </list>
/// <para>
/// Strategy choice mirrors the v0.5.7 generator parser's compatibility table:
/// <c>Guid</c> / <c>long</c> / <c>int</c> emit <c>[OrionId&lt;TValue&gt;]</c> (no strategy
/// required); <c>string</c> emits <c>[OrionId&lt;string, Ulid&gt;]</c> because string
/// requires an explicit strategy.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BareIdPromotionCodeFixProvider))]
[Shared]
public sealed class BareIdPromotionCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY008");

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
        var classDecl = property.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl is null)
        {
            return;
        }
        if (!TryResolveIdShape(property, classDecl, out var newIdName, out var valueTypeKeyword, out var requiresStrategy))
        {
            return;
        }

        // Skip when the proposed id type already exists in the same compilation unit -
        // the user has already promoted, ORIONKEY008 is firing on a stale bare property.
        var existsInFile = root.DescendantNodes().OfType<StructDeclarationSyntax>()
            .Any(s => s.Identifier.ValueText == newIdName);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: existsInFile
                    ? $"Replace '{property.Type}' with '{newIdName}' (id already declared)"
                    : $"Promote '{property.Identifier.ValueText}' to strongly-typed id '{newIdName}'",
                createChangedDocument: ct => PromoteAsync(
                    context.Document, property, newIdName, valueTypeKeyword, requiresStrategy, existsInFile, ct),
                equivalenceKey: $"ORIONKEY008-promote-{newIdName}"),
            diagnostic);
    }

    private static bool TryResolveIdShape(
        PropertyDeclarationSyntax property,
        ClassDeclarationSyntax classDecl,
        out string newIdName,
        out string valueTypeKeyword,
        out bool requiresStrategy)
    {
        newIdName = string.Empty;
        valueTypeKeyword = string.Empty;
        requiresStrategy = false;

        // The analyzer unwraps Nullable<T> when reporting ORIONKEY008 (Guid? Id, long? CustomerId
        // also fire), so the code-fix MUST mirror that. NullableTypeSyntax wraps the underlying
        // type - peel it off before matching.
        var typeToMatch = property.Type is NullableTypeSyntax nullable ? nullable.ElementType : property.Type;
        var (kw, strat) = typeToMatch switch
        {
            PredefinedTypeSyntax { Keyword.ValueText: "long" } => ("long", false),
            PredefinedTypeSyntax { Keyword.ValueText: "int" } => ("int", false),
            PredefinedTypeSyntax { Keyword.ValueText: "string" } => ("string", true),
            IdentifierNameSyntax { Identifier.ValueText: "Guid" } => ("Guid", false),
            QualifiedNameSyntax q when q.Right.Identifier.ValueText == "Guid" => ("Guid", false),
            _ => (string.Empty, false),
        };
        if (kw.Length == 0)
        {
            return false;
        }

        var propertyName = property.Identifier.ValueText;
        newIdName = string.Equals(propertyName, "Id", System.StringComparison.Ordinal)
            ? classDecl.Identifier.ValueText + "Id"
            : propertyName;
        valueTypeKeyword = kw;
        requiresStrategy = strat;
        return true;
    }

    private static async Task<Document> PromoteAsync(
        Document document,
        PropertyDeclarationSyntax property,
        string newIdName,
        string valueTypeKeyword,
        bool requiresStrategy,
        bool idAlreadyDeclared,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilation)
        {
            return document;
        }

        // 1. Rewrite the property type to the new strongly-typed id name. Preserve the
        // original Type's trivia so spacing around the type stays consistent.
        var newType = SyntaxFactory.IdentifierName(newIdName).WithTriviaFrom(property.Type);
        var newProperty = property.WithType(newType);
        var rewritten = compilation.ReplaceNode(property, newProperty);

        // 2. Add the id struct declaration at the end of the compilation unit IF it does
        // not already exist. We emit it as `public readonly partial struct` so other
        // partials can extend it. The OrionId attribute carries the value type as a type
        // argument and (for string) the Ulid strategy.
        if (idAlreadyDeclared)
        {
            return document.WithSyntaxRoot(rewritten);
        }
        // Qualify the attribute + strategy fully so the emitted struct compiles even when
        // the consumer's file does NOT have `using Moongazing.OrionKey;` - the analyzer
        // does not control imports. A simplifier annotation lets the IDE collapse the
        // FQN when the using is present.
        var attributeText = requiresStrategy
            ? $"[global::Moongazing.OrionKey.OrionId<{valueTypeKeyword}, global::Moongazing.OrionKey.Ulid>]"
            : $"[global::Moongazing.OrionKey.OrionId<{valueTypeKeyword}>]";
        var structText = $"\n{attributeText}\npublic readonly partial struct {newIdName};\n";
        var newMember = SyntaxFactory.ParseMemberDeclaration(structText);
        if (newMember is null)
        {
            return document.WithSyntaxRoot(rewritten);
        }

        // Insert into the same NamespaceDeclaration if the original class lived in one,
        // otherwise append to the compilation unit's top-level members. This keeps the
        // generated id in the same namespace as the class, which is required for the
        // [OrionId] generator to wire its converter.
        var classDeclInRewritten = rewritten.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == property.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ValueText);
        SyntaxNode? container = classDeclInRewritten.Parent;
        if (container is BaseNamespaceDeclarationSyntax ns)
        {
            var updated = ns.AddMembers((MemberDeclarationSyntax)newMember);
            return document.WithSyntaxRoot(rewritten.ReplaceNode(ns, updated));
        }
        var updatedRoot = rewritten.AddMembers((MemberDeclarationSyntax)newMember);
        return document.WithSyntaxRoot(updatedRoot);
    }
}
