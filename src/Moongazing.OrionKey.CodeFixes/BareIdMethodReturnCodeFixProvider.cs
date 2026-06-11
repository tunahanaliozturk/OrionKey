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
/// Code fix provider for ORIONKEY011 ("Bare Guid/long/int/string method return type
/// whose name implies an id could be promoted"). Mirrors the v0.5.13
/// BareIdMethodParameterCodeFixProvider behaviour for method return types: rewrites the
/// bare return type to a derived id name (`CreateUserId` Guid -> `UserId`) and emits a
/// sibling <c>[OrionId&lt;TValue&gt;] public readonly partial struct</c> in the same
/// namespace so the next build picks up the generated converter.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BareIdMethodReturnCodeFixProvider))]
[Shared]
public sealed class BareIdMethodReturnCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("ORIONKEY011");

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
        var method = anchor.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null)
        {
            return;
        }

        if (!TryResolveIdShape(method, out var newIdName, out var valueTypeKeyword, out var requiresStrategy))
        {
            return;
        }

        // Scope the existing-id lookup to the method's own namespace (matches v0.5.13).
        var methodNamespace = method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var scopeForLookup = (SyntaxNode?)methodNamespace ?? root;
        var existsInFile = scopeForLookup.DescendantNodes().OfType<StructDeclarationSyntax>()
            .Any(s => s.Identifier.ValueText == newIdName);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: existsInFile
                    ? $"Replace return type '{method.ReturnType}' with '{newIdName}' (id already declared)"
                    : $"Promote '{method.Identifier.ValueText}' return type to strongly-typed id '{newIdName}'",
                createChangedDocument: ct => PromoteAsync(
                    context.Document, method, newIdName, valueTypeKeyword, requiresStrategy, existsInFile, ct),
                equivalenceKey: $"ORIONKEY011-promote-{newIdName}"),
            diagnostic);
    }

    private static bool TryResolveIdShape(
        MethodDeclarationSyntax method,
        out string newIdName,
        out string valueTypeKeyword,
        out bool requiresStrategy)
    {
        newIdName = string.Empty;
        valueTypeKeyword = string.Empty;
        requiresStrategy = false;

        // Mirror the analyzer's unwrap chain: Task<T> / ValueTask<T> -> T, then Nullable<T> -> T.
        // The PROMOTE rewrite preserves the async wrapper (Task<Guid> -> Task<UserId>) and
        // the nullable annotation (Guid? -> UserId?) so the contract surface stays intact.
        var raw = method.ReturnType;
        var asyncOuter = TryUnwrapAsync(raw, out var asyncInner) ? asyncInner! : raw;
        var nullable = asyncOuter is NullableTypeSyntax;
        var typeToMatch = nullable ? ((NullableTypeSyntax)asyncOuter).ElementType : asyncOuter;
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

        // Method name derivation: `CreateUserId` / `GetOrderId` -> the id name IS the
        // matching suffix verbatim. `Id` -> the outer-type-based fallback (`{Class}Id`).
        var methodName = method.Identifier.ValueText;
        if (string.Equals(methodName, "Id", System.StringComparison.Ordinal))
        {
            var containing = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            newIdName = (containing?.Identifier.ValueText ?? "Entity") + "Id";
        }
        else if (methodName.EndsWith("Id", System.StringComparison.Ordinal))
        {
            // CreateUserId -> UserId; GetOrderId -> OrderId; NewSkuId -> SkuId.
            // Strip the leading verb (Create / Get / New / Try / Find / etc.) heuristically:
            // we keep the SUFFIX from the first capital letter that starts the noun phrase.
            // Simplest reliable rule: keep the last PascalCase segment.
            // Implementation: find the last index of an uppercase letter where the preceding
            // char is lowercase or string start; that marks the noun phrase boundary.
            var nounStart = FindNounPhraseStart(methodName);
            newIdName = methodName.Substring(nounStart);
        }
        else
        {
            return false;
        }
        valueTypeKeyword = kw;
        requiresStrategy = strat;
        return true;
    }

    private static int FindNounPhraseStart(string methodName)
    {
        // The noun phrase is the LAST two PascalCase segments joined. "TryGetUserId" ->
        // segments [Try, Get, User, Id] -> last two = "UserId". The result is the index
        // where the second-to-last segment starts.
        // Collect segment-start indices: 0 plus every upper-after-lower boundary.
        var starts = new System.Collections.Generic.List<int> { 0 };
        for (var i = 1; i < methodName.Length; i++)
        {
            if (char.IsUpper(methodName[i]) && char.IsLower(methodName[i - 1]))
            {
                starts.Add(i);
            }
        }
        // We need at least two segments (the noun and "Id"). If the method name is
        // already just "FooId" with a single boundary at the I, return that boundary's
        // start which would mean we keep "Id" alone - degenerate. Return 0 instead so
        // the WHOLE method name becomes the id.
        if (starts.Count < 2)
        {
            return 0;
        }
        return starts[starts.Count - 2];
    }

    private static bool TryUnwrapAsync(TypeSyntax type, out TypeSyntax? inner)
    {
        inner = null;
        if (type is GenericNameSyntax g
            && (g.Identifier.ValueText == "Task" || g.Identifier.ValueText == "ValueTask")
            && g.TypeArgumentList.Arguments.Count == 1)
        {
            inner = g.TypeArgumentList.Arguments[0];
            return true;
        }
        if (type is QualifiedNameSyntax q && q.Right is GenericNameSyntax qg
            && (qg.Identifier.ValueText == "Task" || qg.Identifier.ValueText == "ValueTask")
            && qg.TypeArgumentList.Arguments.Count == 1)
        {
            inner = qg.TypeArgumentList.Arguments[0];
            return true;
        }
        return false;
    }

    private static async Task<Document> PromoteAsync(
        Document document,
        MethodDeclarationSyntax method,
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

        // Build the new return type, preserving Task<T> / ValueTask<T> wrappers and the
        // outer nullable annotation.
        TypeSyntax newInner = SyntaxFactory.IdentifierName(newIdName);
        var raw = method.ReturnType;
        var hadAsync = TryUnwrapAsync(raw, out var asyncInner) ? asyncInner! : null;
        var outerCarrier = hadAsync is null ? raw : hadAsync;
        var nullable = outerCarrier is NullableTypeSyntax;
        if (nullable)
        {
            newInner = SyntaxFactory.NullableType(newInner);
        }
        TypeSyntax newReturnType;
        if (hadAsync is not null)
        {
            // Wrap back in Task<T> / ValueTask<T>. Preserve the QUALIFIED-vs-bare form of
            // the original so a method declared with fully qualified
            // `System.Threading.Tasks.Task<Guid>` stays qualified after the rewrite. A
            // bare-name original (e.g. `Task<Guid>`) is reproduced bare.
            var newTypeArgList = SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(newInner));
            if (raw is QualifiedNameSyntax qualified && qualified.Right is GenericNameSyntax qgeneric)
            {
                var newRight = SyntaxFactory.GenericName(qgeneric.Identifier).WithTypeArgumentList(newTypeArgList);
                newReturnType = SyntaxFactory.QualifiedName(qualified.Left, newRight);
            }
            else
            {
                var asyncIdentifier = (raw as GenericNameSyntax)?.Identifier.ValueText ?? "Task";
                newReturnType = SyntaxFactory.GenericName(SyntaxFactory.Identifier(asyncIdentifier))
                    .WithTypeArgumentList(newTypeArgList);
            }
        }
        else
        {
            newReturnType = newInner;
        }
        newReturnType = newReturnType.WithTriviaFrom(raw);

        var newMethod = method.WithReturnType(newReturnType);
        var rewritten = compilation.ReplaceNode(method, newMethod);

        if (idAlreadyDeclared)
        {
            return document.WithSyntaxRoot(rewritten);
        }

        // Fully-qualify Guid in the emitted attribute. The original file may not have a
        // `using System;` directive (the analyzer matches `System.Guid` qualified usage
        // too), so an unqualified `Guid` in the attribute would fail to compile even
        // when the original code parsed cleanly. Predefined keywords (long/int/string)
        // need no qualification.
        var qualifiedValueType = valueTypeKeyword == "Guid" ? "global::System.Guid" : valueTypeKeyword;
        var attributeText = requiresStrategy
            ? $"[global::Moongazing.OrionKey.OrionId<{qualifiedValueType}, global::Moongazing.OrionKey.Ulid>]"
            : $"[global::Moongazing.OrionKey.OrionId<{qualifiedValueType}>]";
        var structText = $"\n{attributeText}\npublic readonly partial struct {newIdName};\n";
        var newMember = SyntaxFactory.ParseMemberDeclaration(structText);
        if (newMember is null)
        {
            return document.WithSyntaxRoot(rewritten);
        }

        var methodInRewritten = rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == method.Identifier.ValueText);
        SyntaxNode? container = methodInRewritten?.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (container is BaseNamespaceDeclarationSyntax ns)
        {
            var updated = ns.AddMembers((MemberDeclarationSyntax)newMember);
            return document.WithSyntaxRoot(rewritten.ReplaceNode(ns, updated));
        }
        var updatedRoot = rewritten.AddMembers((MemberDeclarationSyntax)newMember);
        return document.WithSyntaxRoot(updatedRoot);
    }
}
