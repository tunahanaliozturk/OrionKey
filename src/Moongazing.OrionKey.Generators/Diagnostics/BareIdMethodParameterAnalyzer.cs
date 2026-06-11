using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY010. Extends ORIONKEY008 to method parameters: flags a method whose parameter
/// is named <c>Id</c> or ends with <c>Id</c> and whose CLR type is <see cref="System.Guid"/>,
/// <see cref="long"/>, <see cref="int"/>, or <see cref="string"/>. Catches mix-up bugs at
/// the call site that ORIONKEY008's property-only check could not.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BareIdMethodParameterAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.BareIdMethodParameterShouldBePromoted);

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

        // Skip compiler-generated methods (delegate Invoke, lambdas, etc.) and operator
        // overloads where the parameter name is conventional, not consumer-meaningful.
        if (method.IsImplicitlyDeclared || method.MethodKind == MethodKind.UserDefinedOperator)
        {
            return;
        }
        if (method.ContainingType is null
            || method.ContainingType.TypeKind == TypeKind.Delegate)
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            if (!IsBareIdName(parameter.Name))
            {
                continue;
            }
            if (!IsBarePrimitiveType(parameter.Type, out var clrTypeName))
            {
                continue;
            }
            ctx.ReportDiagnostic(Diagnostic.Create(
                OrionKeyDiagnostics.BareIdMethodParameterShouldBePromoted,
                parameter.Locations.IsEmpty ? Location.None : parameter.Locations[0],
                method.ContainingType.Name + "." + method.Name,
                parameter.Name,
                clrTypeName));
        }
    }

    private static bool IsBareIdName(string name)
        => string.Equals(name, "id", System.StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("Id", System.StringComparison.Ordinal)
        || name.EndsWith("ID", System.StringComparison.Ordinal);

    private static bool IsBarePrimitiveType(ITypeSymbol type, out string clrTypeName)
    {
        clrTypeName = string.Empty;
        // Unwrap Nullable<T> so `Guid? userId` / `long? orderId` also fire (matches the
        // v0.5.9 ORIONKEY008 behaviour).
        if (type is INamedTypeSymbol named && named.IsGenericType
            && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
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
