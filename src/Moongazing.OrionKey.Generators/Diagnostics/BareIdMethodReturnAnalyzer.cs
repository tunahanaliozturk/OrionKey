using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY011. Mirror of ORIONKEY010 for method return types: flags a method whose
/// return type is <see cref="System.Guid"/>, <see cref="long"/>, <see cref="int"/>, or
/// <see cref="string"/> AND whose name implies an id (`CreateUserId`, `GetOrderId`,
/// `NewSku`, exact `Id`). Promoting the return type keeps the mix-up protection across
/// method boundaries that ORIONKEY010 establishes at parameter boundaries.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BareIdMethodReturnAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.BareIdMethodReturnShouldBePromoted);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(OnMethod, SymbolKind.Method);
    }

    private static void OnMethod(SymbolAnalysisContext ctx)
    {
        var method = (IMethodSymbol)ctx.Symbol;

        // Skip the same compiler-generated and operator-overload cases as ORIONKEY010.
        if (method.IsImplicitlyDeclared || method.MethodKind == MethodKind.UserDefinedOperator)
        {
            return;
        }
        if (method.ContainingType is null
            || method.ContainingType.TypeKind == TypeKind.Delegate)
        {
            return;
        }
        // Constructors and accessors do not have meaningful return types here.
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        if (!ImpliesIdName(method.Name))
        {
            return;
        }
        var returnType = method.ReturnType;
        if (!IsBarePrimitiveType(returnType, out var clrTypeName))
        {
            return;
        }
        ctx.ReportDiagnostic(Diagnostic.Create(
            OrionKeyDiagnostics.BareIdMethodReturnShouldBePromoted,
            method.Locations.IsEmpty ? Location.None : method.Locations[0],
            method.ContainingType.Name,
            method.Name,
            clrTypeName));
    }

    private static bool ImpliesIdName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        // Exact "Id" or PascalCase suffix "...Id". We deliberately exclude method names
        // that contain "Id" elsewhere (e.g. "Identify", "Hide", "Provider") to keep the
        // signal-to-noise ratio high.
        return name == "Id" || (name.Length > 2 && name.EndsWith("Id", System.StringComparison.Ordinal));
    }

    private static bool IsBarePrimitiveType(ITypeSymbol type, out string clrTypeName)
    {
        clrTypeName = string.Empty;
        // Unwrap Nullable<T> so methods returning `Guid?` also fire - the mix-up risk
        // is identical regardless of whether the return type is value or value? .
        if (type is INamedTypeSymbol named && named.IsGenericType
            && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        // Skip async return types: Task<T> / ValueTask<T> wrap the real return; unwrap
        // them and recurse on T so `Task<Guid> CreateUserId()` also fires.
        if (type is INamedTypeSymbol awaiterNamed && awaiterNamed.IsGenericType
            && awaiterNamed.TypeArguments.Length == 1
            && (awaiterNamed.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.Task<TResult>"
                || awaiterNamed.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>"))
        {
            type = awaiterNamed.TypeArguments[0];
            // Re-apply Nullable unwrap on the inner type.
            if (type is INamedTypeSymbol innerNullable && innerNullable.IsGenericType
                && innerNullable.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
                && innerNullable.TypeArguments.Length == 1)
            {
                type = innerNullable.TypeArguments[0];
            }
        }
        switch (type.SpecialType)
        {
            case SpecialType.System_Int64:
                clrTypeName = "long";
                return true;
            case SpecialType.System_Int32:
                clrTypeName = "int";
                return true;
            case SpecialType.System_String:
                clrTypeName = "string";
                return true;
        }
        if (type.ToDisplayString() == "System.Guid")
        {
            clrTypeName = "System.Guid";
            return true;
        }
        return false;
    }
}
