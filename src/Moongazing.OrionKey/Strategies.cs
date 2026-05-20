namespace Moongazing.OrionKey;

/// <summary>Strategy marker: 64-bit Twitter-Snowflake ids (sortable). Pairs with <see cref="long"/>.</summary>
public readonly struct Snowflake;

/// <summary>Strategy marker: 26-character ULID strings (sortable). Pairs with <see cref="string"/>.</summary>
public readonly struct Ulid;

/// <summary>Strategy marker: 21-character NanoId strings. Pairs with <see cref="string"/>.</summary>
public readonly struct NanoId;

/// <summary>Strategy marker: version-7 GUIDs (sortable). Pairs with <see cref="System.Guid"/>.</summary>
public readonly struct GuidV7;
