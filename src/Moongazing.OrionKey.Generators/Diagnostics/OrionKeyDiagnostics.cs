using Microsoft.CodeAnalysis;

namespace Moongazing.OrionKey.Generators.Diagnostics;

internal static class OrionKeyDiagnostics
{
    private const string Category = "OrionKey";

    public static readonly DiagnosticDescriptor NotReadonlyPartialStruct = new(
        "ORIONKEY001", "OrionId target must be a readonly partial struct",
        "'{0}' is marked [OrionId] but is not a 'readonly partial struct'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedValueType = new(
        "ORIONKEY002", "Unsupported OrionId value or strategy type",
        "'{0}' is not a supported [OrionId] value type or strategy",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StringRequiresStrategy = new(
        "ORIONKEY003", "string OrionId requires an explicit strategy",
        "'{0}' uses a string value type, which requires an explicit strategy (Ulid or NanoId)",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatibleStrategy = new(
        "ORIONKEY004", "Incompatible OrionId strategy",
        "Strategy '{0}' is not compatible with value type '{1}'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberCollision = new(
        "ORIONKEY005", "OrionId struct declares a generated member",
        "'{0}' declares a member named '{1}' that the OrionId generator also emits",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
