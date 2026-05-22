namespace Moongazing.OrionKey.Generators.Model;

internal enum StrategyType
{
    /// <summary>No strategy supplied. Guid -> Guid.NewGuid(); int/long -> externally assigned.</summary>
    None,
    Snowflake,
    Ulid,
    NanoId,
    GuidV7,
    Cuid2,
    Ksuid,
    ObjectId,
    SequentialGuid,
}
