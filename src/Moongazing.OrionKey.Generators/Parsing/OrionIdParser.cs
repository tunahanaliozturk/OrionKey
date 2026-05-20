using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Moongazing.OrionKey.Generators.Diagnostics;
using Moongazing.OrionKey.Generators.Model;
using ValueType = Moongazing.OrionKey.Generators.Model.ValueType;

namespace Moongazing.OrionKey.Generators.Parsing;

/// <summary>Turns an <c>[OrionId]</c>-decorated type symbol into an <see cref="OrionIdModel"/>.</summary>
internal static class OrionIdParser
{
    public static bool TryParse(
        INamedTypeSymbol symbol,
        out OrionIdModel? model,
        out IReadOnlyList<Diagnostic> diagnostics)
    {
        var diags = new List<Diagnostic>();
        model = null;
        diagnostics = diags;

        var attribute = FindOrionIdAttribute(symbol);
        if (attribute is null)
        {
            return false;
        }

        if (!IsReadonlyPartialStruct(symbol))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.NotReadonlyPartialStruct,
                symbol.Locations[0], symbol.Name));
            return false;
        }

        var typeArgs = attribute.AttributeClass!.TypeArguments;
        var valueSymbol = typeArgs[0];
        var strategySymbol = typeArgs.Length == 2 ? typeArgs[1] : null;

        if (!TryMapValueType(valueSymbol, out var valueType))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.UnsupportedValueType,
                symbol.Locations[0], valueSymbol.ToDisplayString()));
            return false;
        }

        var strategy = StrategyType.None;
        if (strategySymbol is not null && !TryMapStrategy(strategySymbol, out strategy))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.UnsupportedValueType,
                symbol.Locations[0], strategySymbol.ToDisplayString()));
            return false;
        }

        if (valueType == ValueType.String && strategy == StrategyType.None)
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.StringRequiresStrategy,
                symbol.Locations[0], symbol.Name));
            return false;
        }

        if (!IsCompatible(valueType, strategy))
        {
            diags.Add(Diagnostic.Create(OrionKeyDiagnostics.IncompatibleStrategy,
                symbol.Locations[0], strategy.ToString(), valueType.ToString()));
            return false;
        }

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        model = new OrionIdModel(symbol.Name, ns, valueType, strategy);
        return true;
    }

    private static AttributeData? FindOrionIdAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null || !attrClass.IsGenericType)
            {
                continue;
            }
            var name = attrClass.ConstructUnboundGenericType().ToDisplayString();
            if (name is "Moongazing.OrionKey.OrionIdAttribute<>"
                     or "Moongazing.OrionKey.OrionIdAttribute<,>")
            {
                return attr;
            }
        }
        return null;
    }

    private static bool IsReadonlyPartialStruct(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Struct || !symbol.IsReadOnly)
        {
            return false;
        }
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax s
                && s.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryMapValueType(ITypeSymbol symbol, out ValueType valueType)
    {
        valueType = default;
        switch (symbol.SpecialType)
        {
            case SpecialType.System_Int32: valueType = ValueType.Int32; return true;
            case SpecialType.System_Int64: valueType = ValueType.Int64; return true;
            case SpecialType.System_String: valueType = ValueType.String; return true;
        }
        if (symbol.ToDisplayString() == "System.Guid")
        {
            valueType = ValueType.Guid;
            return true;
        }
        return false;
    }

    private static bool TryMapStrategy(ITypeSymbol symbol, out StrategyType strategy)
    {
        strategy = symbol.ToDisplayString() switch
        {
            "Moongazing.OrionKey.Snowflake" => StrategyType.Snowflake,
            "Moongazing.OrionKey.Ulid" => StrategyType.Ulid,
            "Moongazing.OrionKey.NanoId" => StrategyType.NanoId,
            "Moongazing.OrionKey.GuidV7" => StrategyType.GuidV7,
            _ => StrategyType.None,
        };
        return strategy != StrategyType.None;
    }

    private static bool IsCompatible(ValueType value, StrategyType strategy) => strategy switch
    {
        StrategyType.None => value is ValueType.Guid or ValueType.Int32 or ValueType.Int64,
        StrategyType.Snowflake => value == ValueType.Int64,
        StrategyType.Ulid => value == ValueType.String,
        StrategyType.NanoId => value == ValueType.String,
        StrategyType.GuidV7 => value == ValueType.Guid,
        _ => false,
    };
}
