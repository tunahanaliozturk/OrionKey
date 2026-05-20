namespace Moongazing.OrionKey.Generators.Model;

/// <summary>Immutable parsed description of one <c>[OrionId]</c>-decorated struct.</summary>
internal sealed record OrionIdModel(
    string Name,
    string Namespace,
    ValueType ValueType,
    StrategyType Strategy)
{
    /// <summary>True when the struct should get a static <c>New()</c> factory.</summary>
    public bool GeneratesNew => Strategy != StrategyType.None || ValueType == ValueType.Guid;

    /// <summary>True when the strategy produces creation-ordered ids (gets <c>IComparable</c>).</summary>
    public bool IsSortable => Strategy is StrategyType.Snowflake or StrategyType.Ulid or StrategyType.GuidV7;

    /// <summary>The fully-qualified C# keyword/type of the underlying value.</summary>
    public string ValueKeyword => ValueType switch
    {
        ValueType.Guid => "global::System.Guid",
        ValueType.Int32 => "int",
        ValueType.Int64 => "long",
        ValueType.String => "string",
        _ => throw new System.ArgumentOutOfRangeException(nameof(ValueType)),
    };
}
