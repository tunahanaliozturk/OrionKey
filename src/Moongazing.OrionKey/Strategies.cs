namespace Moongazing.OrionKey;

/// <summary>Strategy marker: 64-bit Twitter-Snowflake ids (sortable). Pairs with <see cref="long"/>.</summary>
public readonly struct Snowflake;

/// <summary>Strategy marker: 26-character ULID strings (sortable). Pairs with <see cref="string"/>.</summary>
public readonly struct Ulid;

/// <summary>Strategy marker: 21-character NanoId strings. Pairs with <see cref="string"/>.</summary>
public readonly struct NanoId;

/// <summary>Strategy marker: version-7 GUIDs (sortable). Pairs with <see cref="System.Guid"/>.</summary>
public readonly struct GuidV7;

/// <summary>Strategy marker: 24-character CUID2 strings. Pairs with <see cref="string"/>.</summary>
public readonly struct Cuid2;

/// <summary>Strategy marker: 27-character KSUID strings (sortable). Pairs with <see cref="string"/>.</summary>
public readonly struct Ksuid;

/// <summary>Strategy marker: 24-character hex MongoDB ObjectId strings (sortable). Pairs with <see cref="string"/>.</summary>
public readonly struct ObjectId;

/// <summary>Strategy marker: index-friendly sequential GUIDs (sortable). Pairs with <see cref="System.Guid"/>.</summary>
public readonly struct SequentialGuid;
