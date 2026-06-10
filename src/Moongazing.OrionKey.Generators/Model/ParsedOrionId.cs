namespace Moongazing.OrionKey.Generators.Model;

/// <summary>
/// Cache-friendly parsed pipeline element. Pairs the (nullable) <see cref="OrionIdModel"/>
/// with the diagnostics produced during parsing as <see cref="DiagnosticInfo"/> so the
/// whole pair has structural equality and the Roslyn incremental cache can reuse it
/// across recompiles when the input hasn't changed.
/// </summary>
internal readonly record struct ParsedOrionId(
    OrionIdModel? Model,
    EquatableArray<DiagnosticInfo> Diagnostics);
