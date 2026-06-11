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
/// Code fix provider for ORIONKEY010 ("Bare Guid/long/int/string method parameter named
/// Id or *Id could be promoted to a strongly-typed id"). Mirrors the v0.5.9
/// BareIdPromotionCodeFixProvider behaviour for property declarations: rewrites the
/// parameter's type to a derived id name and emits a sibling
/// <c>[OrionId&lt;TValue&gt;] public readonly partial struct</c> in the same compilation
/// unit so the next build picks up the generated converter automatically.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BareIdMethodParameterCodeFixProvider))]
[Shared]
public sealed class BareIdMethodParameterCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY010");

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
        var parameter = anchor.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
        if (parameter is null || parameter.Type is null)
        {
            return;
        }

        if (!TryResolveIdShape(parameter, out var newIdName, out var valueTypeKeyword, out var requiresStrategy))
        {
            return;
        }

        var existsInFile = root.DescendantNodes().OfType<StructDeclarationSyntax>()
            .Any(s => s.Identifier.ValueText == newIdName);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: existsInFile
                    ? $"Replace '{parameter.Type}' with '{newIdName}' (id already declared)"
                    : $"Promote '{parameter.Identifier.ValueText}' parameter to strongly-typed id '{newIdName}'",
                createChangedDocument: ct => PromoteAsync(
                    context.Document, parameter, newIdName, valueTypeKeyword, requiresStrategy, existsInFile, ct),
                equivalenceKey: $"ORIONKEY010-promote-{newIdName}"),
            diagnostic);
    }

    private static bool TryResolveIdShape(
        ParameterSyntax parameter,
        out string newIdName,
        out string valueTypeKeyword,
        out bool requiresStrategy)
    {
        newIdName = string.Empty;
        valueTypeKeyword = string.Empty;
        requiresStrategy = false;

        // Mirror the v0.5.12 analyzer's Nullable<T> unwrap so `Guid? userId` also fixes.
        var typeToMatch = parameter.Type is NullableTypeSyntax nullable ? nullable.ElementType : parameter.Type!;
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

        // Derive the id name from the parameter name. "id" -> outer-type-based fallback
        // would require resolving the containing method's symbol context; for parameters
        // the simpler rule is: lower-case "id" -> "Id" (rare); "*Id" -> capitalised
        // verbatim. The v0.5.13 fix keeps the analyzer's parameter-name unchanged in the
        // signature - only the TYPE gets rewritten.
        var paramName = parameter.Identifier.ValueText;
        newIdName = string.Equals(paramName, "id", System.StringComparison.OrdinalIgnoreCase)
            ? "Id"
            : char.ToUpper(paramName[0], System.Globalization.CultureInfo.InvariantCulture) + paramName.Substring(1);
        valueTypeKeyword = kw;
        requiresStrategy = strat;
        return true;
    }

    private static async Task<Document> PromoteAsync(
        Document document,
        ParameterSyntax parameter,
        string newIdName,
        string valueTypeKeyword,
        bool requiresStrategy,
        bool idAlreadyDeclared,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilation || parameter.Type is null)
        {
            return document;
        }

        var newType = SyntaxFactory.IdentifierName(newIdName).WithTriviaFrom(parameter.Type);
        var newParameter = parameter.WithType(newType);
        var rewritten = compilation.ReplaceNode(parameter, newParameter);

        if (idAlreadyDeclared)
        {
            return document.WithSyntaxRoot(rewritten);
        }

        var attributeText = requiresStrategy
            ? $"[global::Moongazing.OrionKey.OrionId<{valueTypeKeyword}, global::Moongazing.OrionKey.Ulid>]"
            : $"[global::Moongazing.OrionKey.OrionId<{valueTypeKeyword}>]";
        var structText = $"\n{attributeText}\npublic readonly partial struct {newIdName};\n";
        var newMember = SyntaxFactory.ParseMemberDeclaration(structText);
        if (newMember is null)
        {
            return document.WithSyntaxRoot(rewritten);
        }

        // Insert into the same NamespaceDeclaration if the containing method lived in one,
        // otherwise append to the compilation unit's top-level members. The struct must
        // sit in a namespace the consumer's `using` directives can resolve.
        var parameterListInRewritten = rewritten.DescendantNodes().OfType<ParameterSyntax>()
            .FirstOrDefault(p => p.Identifier.ValueText == parameter.Identifier.ValueText
                                 && p.Type is IdentifierNameSyntax id && id.Identifier.ValueText == newIdName);
        SyntaxNode? container = parameterListInRewritten?.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (container is BaseNamespaceDeclarationSyntax ns)
        {
            var updated = ns.AddMembers((MemberDeclarationSyntax)newMember);
            return document.WithSyntaxRoot(rewritten.ReplaceNode(ns, updated));
        }
        var updatedRoot = rewritten.AddMembers((MemberDeclarationSyntax)newMember);
        return document.WithSyntaxRoot(updatedRoot);
    }
}
