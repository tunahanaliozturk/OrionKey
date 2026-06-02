using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY007. Flags a <c>[OrionId]</c> struct that is declared but never referenced
/// anywhere else in the compilation.
/// </summary>
/// <remarks>
/// What counts as "referenced": any syntax-level identifier resolving to the struct from
/// a node that is not the declaration itself. That includes property types, field types,
/// method parameters, return types, generic type arguments, <c>typeof()</c>, <c>nameof()</c>,
/// attribute arguments, and member accesses such as <c>OrderId.New()</c> or
/// <c>OrderId.Empty</c>. Any of these suffices - usage via EF Core converters, JSON
/// converters, ASP.NET binding, Dapper, etc. all reduce to a syntactic reference somewhere
/// in user code.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedOrionIdAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.DeclaredButUnused);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // We deliberately scan generated trees too, because user code may register the id
        // in source-generator output (e.g. a Mediator handler). Excluding generated code
        // here would produce false positives.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext start)
    {
        if (!OrionIdTypeIndex.ReferencesOrionKey(start.Compilation))
        {
            return;
        }

        var declared = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);
        var referenced = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);

        start.RegisterSymbolAction(symbolCtx =>
        {
            var named = (INamedTypeSymbol)symbolCtx.Symbol;
            if (OrionIdTypeIndex.IsOrionIdType(named))
            {
                declared[named] = 1;
            }
        }, SymbolKind.NamedType);

        start.RegisterSemanticModelAction(semanticCtx =>
        {
            var model = semanticCtx.SemanticModel;
            var root = model.SyntaxTree.GetRoot(semanticCtx.CancellationToken);

            foreach (var node in root.DescendantNodes())
            {
                // Skip the type's own declaration node so the declaration itself does not
                // count as a self-reference.
                if (node is StructDeclarationSyntax)
                {
                    continue;
                }

                INamedTypeSymbol? resolved = null;

                switch (node)
                {
                    case IdentifierNameSyntax id:
                        resolved = model.GetSymbolInfo(id, semanticCtx.CancellationToken).Symbol as INamedTypeSymbol;
                        break;
                    case GenericNameSyntax generic:
                        resolved = model.GetSymbolInfo(generic, semanticCtx.CancellationToken).Symbol as INamedTypeSymbol;
                        break;
                    case QualifiedNameSyntax qn:
                        resolved = model.GetSymbolInfo(qn, semanticCtx.CancellationToken).Symbol as INamedTypeSymbol;
                        break;
                    case MemberAccessExpressionSyntax member:
                        resolved = model.GetSymbolInfo(member.Expression, semanticCtx.CancellationToken).Symbol as INamedTypeSymbol;
                        break;
                }

                if (resolved is null)
                {
                    continue;
                }
                if (!OrionIdTypeIndex.IsOrionIdType(resolved))
                {
                    continue;
                }

                referenced[resolved] = 1;
            }
        });

        start.RegisterCompilationEndAction(end =>
        {
            foreach (var entry in declared)
            {
                var symbol = entry.Key;
                if (referenced.ContainsKey(symbol))
                {
                    continue;
                }

                var location = symbol.Locations.FirstOrDefault();
                if (location is null)
                {
                    continue;
                }

                end.ReportDiagnostic(Diagnostic.Create(
                    OrionKeyDiagnostics.DeclaredButUnused,
                    location,
                    symbol.Name));
            }
        });
    }
}
