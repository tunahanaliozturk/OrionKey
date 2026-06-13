using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Moongazing.OrionKey.Generators.Diagnostics;
using Moongazing.OrionKey.Generators.Model;
using ValueType = Moongazing.OrionKey.Generators.Model.ValueType;

namespace Moongazing.OrionKey.Generators.Parsing;

/// <summary>Turns an <c>[OrionId]</c>-decorated type symbol into an <see cref="OrionIdModel"/>.</summary>
internal static class OrionIdParser
{
    /// <summary>
    /// Parse a symbol into a cache-friendly <see cref="ParsedOrionId"/>. This is the entry
    /// point used by the v0.5.5 incremental pipeline; the result is value-equatable so
    /// Roslyn can cache it across recompiles.
    /// </summary>
    public static ParsedOrionId Parse(INamedTypeSymbol symbol)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var model = ParseCore(symbol, diagnostics);
        return new ParsedOrionId(model, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    /// <summary>
    /// Legacy <see cref="Diagnostic"/>-based entry point retained for the analyzer surfaces
    /// that still consume <see cref="Diagnostic"/> directly. Callers on the incremental
    /// pipeline should prefer <see cref="Parse(INamedTypeSymbol)"/>.
    /// </summary>
    public static bool TryParse(
        INamedTypeSymbol symbol,
        out OrionIdModel? model,
        out IReadOnlyList<Diagnostic> diagnostics)
    {
        var parsed = Parse(symbol);
        model = parsed.Model;
        var converted = new List<Diagnostic>(parsed.Diagnostics.Count);
        foreach (var diag in parsed.Diagnostics)
        {
            converted.Add(diag.ToDiagnostic());
        }
        diagnostics = converted;
        return model is not null;
    }

    private static OrionIdModel? ParseCore(INamedTypeSymbol symbol, ImmutableArray<DiagnosticInfo>.Builder diags)
    {
        var attribute = FindOrionIdAttribute(symbol);
        if (attribute is null)
        {
            return null;
        }

        var location = symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;

        if (!IsReadonlyPartialStruct(symbol))
        {
            diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.NotReadonlyPartialStruct, location, symbol.Name));
            return null;
        }

        var typeArgs = attribute.AttributeClass!.TypeArguments;
        var valueSymbol = typeArgs[0];
        var strategySymbol = typeArgs.Length == 2 ? typeArgs[1] : null;

        if (!TryMapValueType(valueSymbol, out var valueType))
        {
            diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.UnsupportedValueType, location, valueSymbol.ToDisplayString()));
            return null;
        }

        var strategy = StrategyType.None;
        if (strategySymbol is not null && !TryMapStrategy(strategySymbol, out strategy))
        {
            diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.UnsupportedValueType, location, strategySymbol.ToDisplayString()));
            return null;
        }

        if (valueType == ValueType.String && strategy == StrategyType.None)
        {
            diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.StringRequiresStrategy, location, symbol.Name));
            return null;
        }

        if (!IsCompatible(valueType, strategy))
        {
            diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.IncompatibleStrategy, location, strategy.ToString(), valueType.ToString()));
            return null;
        }

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();

        var model = new OrionIdModel(symbol.Name, ns, valueType, strategy)
        {
            ConsumerHasDebuggerDisplay = HasConsumerDeclaredDebuggerDisplay(symbol),
        };
        CheckMemberCollisions(symbol, model, diags, location);
        return model;
    }

    private static bool HasConsumerDeclaredDebuggerDisplay(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            if (name == "System.Diagnostics.DebuggerDisplayAttribute")
            {
                return true;
            }
        }
        return false;
    }

    private static void CheckMemberCollisions(
        INamedTypeSymbol symbol, OrionIdModel model, ImmutableArray<DiagnosticInfo>.Builder diags, Location location)
    {
        // v0.5.20: IsEmpty added to the collision list because the generator emits it
        // unconditionally and a consumer-declared IsEmpty would produce a duplicate
        // member error post-generation.
        var emitted = new List<string> { "Value", "Empty", "IsEmpty", "WrapAll", "UnwrapAll", "Equals", "GetHashCode", "ToString" };
        // v0.5.22 fix (codex P2): only gate New and CreateMany when the model actually
        // emits them. External-id strategies ([OrionId<int>] without strategy) leave
        // GeneratesNew=false and the emitter skips both factories - colliding on a
        // consumer-declared CreateMany in that case is a false positive.
        if (model.GeneratesNew)
        {
            emitted.Add("New");
            emitted.Add("CreateMany");
        }
        // ParseOrDefault only emitted for primitive-backed (v0.5.21 fix).
        if (model.ValueType != ValueType.String)
        {
            emitted.Add("ParseOrDefault");
        }
        // v0.5.26: CompareTo is now emitted for ALL id types (was sortable-only), so it
        // is unconditionally on the collision list.
        emitted.Add("CompareTo");
        foreach (var memberName in emitted)
        {
            if (!symbol.GetMembers(memberName).IsEmpty)
            {
                diags.Add(DiagnosticInfo.From(OrionKeyDiagnostics.MemberCollision, location, symbol.Name, memberName));
            }
        }
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
            "Moongazing.OrionKey.Cuid2" => StrategyType.Cuid2,
            "Moongazing.OrionKey.Ksuid" => StrategyType.Ksuid,
            "Moongazing.OrionKey.ObjectId" => StrategyType.ObjectId,
            "Moongazing.OrionKey.SequentialGuid" => StrategyType.SequentialGuid,
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
        StrategyType.Cuid2 => value == ValueType.String,
        StrategyType.Ksuid => value == ValueType.String,
        StrategyType.ObjectId => value == ValueType.String,
        StrategyType.SequentialGuid => value == ValueType.Guid,
        _ => false,
    };
}
