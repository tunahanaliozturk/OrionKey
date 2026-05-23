namespace Moongazing.OrionKey.Generators;

/// <summary>
/// Snapshot of which optional integration packages the consumer compilation references.
/// Computed once per compilation; passed into the source-output pipeline so emitters can
/// fire only when their target package is present.
/// </summary>
internal readonly record struct IntegrationFlags(
    bool HasEfCore,
    bool HasDapper,
    bool HasNewtonsoftJson,
    bool HasMongo,
    bool HasSwashbuckle);
