using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// Shared helper for analyzers that need to identify which named types in a compilation
/// are decorated with <c>[OrionId&lt;...&gt;]</c>. Centralised so ORIONKEY006 and ORIONKEY007
/// use exactly the same detection rule as the source generator's parser.
/// </summary>
internal static class OrionIdTypeIndex
{
    private const string OneArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`1";
    private const string TwoArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`2";

    /// <summary>Returns <c>true</c> when the symbol is decorated with any arity of <c>[OrionId]</c>.</summary>
    public static bool IsOrionIdType(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null || !attrClass.IsGenericType)
            {
                continue;
            }

            var unbound = attrClass.ConstructUnboundGenericType().ToDisplayString();
            if (unbound is "Moongazing.OrionKey.OrionIdAttribute<>"
                       or "Moongazing.OrionKey.OrionIdAttribute<,>")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the supplied <see cref="Compilation"/> references the OrionKey
    /// runtime assembly (and therefore <c>OrionIdAttribute</c> is resolvable). Analyzers
    /// short-circuit out when this is false to avoid wasted work in unrelated projects.
    /// </summary>
    public static bool ReferencesOrionKey(Compilation compilation)
    {
        return compilation.GetTypeByMetadataName(OneArgAttribute) is not null
            || compilation.GetTypeByMetadataName(TwoArgAttribute) is not null;
    }

    /// <summary>Eagerly enumerates every <c>[OrionId]</c> type declared in the compilation.</summary>
    public static IEnumerable<INamedTypeSymbol> EnumerateDeclared(Compilation compilation)
    {
        return Visit(compilation.Assembly.GlobalNamespace);

        static IEnumerable<INamedTypeSymbol> Visit(INamespaceOrTypeSymbol root)
        {
            foreach (var member in root.GetMembers())
            {
                switch (member)
                {
                    case INamespaceSymbol ns:
                        foreach (var nested in Visit(ns))
                        {
                            yield return nested;
                        }
                        break;
                    case INamedTypeSymbol named when IsOrionIdType(named):
                        yield return named;
                        break;
                    case INamedTypeSymbol named:
                        foreach (var nested in Visit(named))
                        {
                            yield return nested;
                        }
                        break;
                }
            }
        }
    }
}
