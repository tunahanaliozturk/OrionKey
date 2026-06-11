using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY012. Flags entity properties whose name is identical to the OrionId type name
/// (`UserId UserId`, `OrderId OrderId`). The property name does not add information beyond
/// the type; renaming to the unprefixed form (`Id` for the entity's own id) keeps entity
/// APIs readable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantOrionIdPropertyNameAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.RedundantOrionIdPropertyName);

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

        if (property.IsIndexer || property.IsStatic)
        {
            return;
        }
        // Property name must equal the property type name AND the type must be an OrionId.
        if (property.Type is not INamedTypeSymbol typeSymbol)
        {
            return;
        }
        if (!string.Equals(property.Name, typeSymbol.Name, System.StringComparison.Ordinal))
        {
            return;
        }
        if (!OrionIdTypeIndex.IsOrionIdType(typeSymbol))
        {
            return;
        }
        // The owning type's OWN primary id should be named just "Id". A foreign-key
        // property (different entity's id) should keep the type-name without the
        // entity prefix - e.g. an Order entity referencing the User entity would
        // have an OrderId Id (own) and a UserId UserId... pointing at User. The
        // analyzer's recommended rename is:
        //   - If the containing type's name + "Id" equals the type name -> rename to "Id".
        //   - Otherwise -> we still flag but suggest dropping the redundant doubling.
        var containing = property.ContainingType;
        if (containing is null)
        {
            return;
        }
        string suggested;
        if (string.Equals(typeSymbol.Name, containing.Name + "Id", System.StringComparison.Ordinal))
        {
            suggested = "Id";
        }
        else
        {
            // Strip the trailing "Id" if present, otherwise keep the type name unchanged.
            // For `UserId UserId` on an Order, suggest renaming to `User` (the relation).
            suggested = typeSymbol.Name.EndsWith("Id", System.StringComparison.Ordinal)
                ? typeSymbol.Name.Substring(0, typeSymbol.Name.Length - 2)
                : typeSymbol.Name;
            if (string.IsNullOrEmpty(suggested))
            {
                return;
            }
        }
        var location = property.Locations.FirstOrDefault();
        if (location is null)
        {
            return;
        }
        ctx.ReportDiagnostic(Diagnostic.Create(
            OrionKeyDiagnostics.RedundantOrionIdPropertyName,
            location,
            containing.Name,
            property.Name,
            typeSymbol.Name,
            suggested));
    }
}
