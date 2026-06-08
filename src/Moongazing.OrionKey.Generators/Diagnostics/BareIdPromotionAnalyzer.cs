using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY008. Suggests promoting a property whose name is <c>Id</c> or ends with <c>Id</c>
/// and whose CLR type is <see cref="System.Guid"/>, <see cref="long"/>, <see cref="int"/>, or
/// <see cref="string"/> to a strongly-typed id via <c>[OrionId]</c>.
/// </summary>
/// <remarks>
/// Severity is Info. The analyzer deliberately reports on the property symbol rather than the
/// declaration syntax so it works on partial classes, records, and class-level fluent property
/// declarations. Consumers tune via <c>.editorconfig</c> when a legacy area should keep
/// primitives.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BareIdPromotionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.BareIdShouldBePromoted);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(OnProperty, SymbolKind.Property);
    }

    private static void OnProperty(SymbolAnalysisContext ctx)
    {
        var property = (IPropertySymbol)ctx.Symbol;

        // Skip indexers, static properties, and properties without a clear declared name we can flag.
        if (property.IsIndexer || property.IsStatic)
        {
            return;
        }

        // Name must be exactly "Id" or end with "Id" (case-sensitive to match the .NET property convention).
        if (!IsIdShaped(property.Name))
        {
            return;
        }

        // Type filter: Guid, long, int, string. Other types (DateTime, decimal, byte[]) are not
        // candidates for a typed id in v0.5 OrionKey.
        var type = property.Type;
        if (!IsCandidateType(type))
        {
            return;
        }

        // Skip properties that are themselves on an OrionId struct (recursive false positives) or
        // that already use an [OrionId]-decorated struct. OrionIdTypeIndex.IsOrionIdType handles both.
        if (OrionIdTypeIndex.IsOrionIdType(property.ContainingType)
            || (type is INamedTypeSymbol named && OrionIdTypeIndex.IsOrionIdType(named)))
        {
            return;
        }

        var location = property.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }

        ctx.ReportDiagnostic(Diagnostic.Create(
            OrionKeyDiagnostics.BareIdShouldBePromoted,
            location,
            property.ContainingType?.Name ?? "?",
            property.Name,
            type.ToDisplayString()));
    }

    private static bool IsIdShaped(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }
        // Exact "Id" or PascalCase suffix "...Id".
        return name == "Id" || (name.Length > 2 && name.EndsWith("Id", System.StringComparison.Ordinal));
    }

    private static bool IsCandidateType(ITypeSymbol type)
    {
        // Strip the reference-type nullability annotation so int? (reference) still matches int.
        // For value types wrapped in Nullable<T> (Guid?, long?), unwrap to the underlying type
        // because Nullable<T> is a distinct INamedTypeSymbol from T.
        var t = type.WithNullableAnnotation(NullableAnnotation.None);
        if (t is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            t = named.TypeArguments[0];
        }

        return t.SpecialType is SpecialType.System_Int32
                                or SpecialType.System_Int64
                                or SpecialType.System_String
            || (t is INamedTypeSymbol n && n.ToDisplayString() == "System.Guid");
    }
}
