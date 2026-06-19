namespace Moongazing.OrionKey.IntegrationTests;

/// <summary>
/// xUnit runs distinct test classes in parallel by default. Dapper resolves type handlers from a
/// process-wide mutable static (<c>Dapper.SqlMapper</c>), and registering a handler replaces that
/// table via copy-on-write. Two classes mutating it concurrently can therefore race: one class can
/// publish a snapshot that does not include the handlers the other just added, dropping a handler
/// and making an already-registered id surface as
/// <c>NotSupportedException: the member ... cannot be used as a parameter value</c> at query time.
///
/// Assigning every Dapper-static-mutating class to this single collection serializes them (xUnit
/// never parallelizes within one collection) while leaving the rest of the suite parallel. This is
/// a test-isolation fix only: it removes the race without weakening any assertion - each test still
/// registers the handlers and proves every id binds as a Dapper parameter.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DapperStaticStateCollectionMarker
{
    public const string Name = "Dapper static SqlMapper state";
}
