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
        "'{0}' uses a string value type, which requires an explicit strategy",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IncompatibleStrategy = new(
        "ORIONKEY004", "Incompatible OrionId strategy",
        "Strategy '{0}' is not compatible with value type '{1}'",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberCollision = new(
        "ORIONKEY005", "OrionId struct declares a generated member",
        "'{0}' declares a member named '{1}' that the OrionId generator also emits",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EfKeyWithoutConversion = new(
        "ORIONKEY006", "OrionId entity key has no EF Core HasConversion call",
        "Entity property '{0}.{1}' is an OrionId type but no HasConversion / HasOrionKeyConversion " +
        "call was found. Wire it in IEntityTypeConfiguration.Configure with " +
        "'builder.Property(x => x.{1}).HasConversion(new {2}ValueConverter())'.",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "OrionKey ships a generated ValueConverter for every [OrionId] struct. EF Core " +
        "will not auto-discover it; it must be wired with HasConversion or HasOrionKeyConversion on " +
        "the property or via a model-builder convention. Without it EF maps the struct as an owned " +
        "type and primary-key/foreign-key behaviour is broken.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor DeclaredButUnused = new(
        "ORIONKEY007", "OrionId struct is declared but never referenced",
        "'{0}' is declared with [OrionId] but is not referenced anywhere in this compilation",
        Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
        description: "A strongly-typed id that is never used is dead code. This usually means the " +
        "id was generated but the call sites that should consume it still use bare Guid/long/string. " +
        "Either reference the id from an entity, DTO, or parameter, or remove the declaration.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public static readonly DiagnosticDescriptor BareIdShouldBePromoted = new(
        "ORIONKEY008", "Bare Guid/long property named Id or *Id could be promoted to a strongly-typed id",
        "Property '{0}.{1}' has CLR type '{2}' and name '{1}'; consider replacing it with an [OrionId] " +
        "strongly-typed id so primary-key/foreign-key bugs become compile-time errors",
        Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
        description: "A property whose name matches 'Id' or ends with 'Id' and whose CLR type is " +
        "Guid / long / int / string is a strong candidate for an [OrionId] strongly-typed id. " +
        "Strongly-typed ids prevent mixing OrderId with CustomerId in a method signature and let " +
        "OrionKey's EF Core ValueConverter handle persistence transparently. This diagnostic is " +
        "advisory (Info severity); tune via .editorconfig if you have a legacy area that should stay " +
        "on primitives.");
}
