namespace Moongazing.OrionKey;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a strongly-typed id backed by <typeparamref name="TValue"/>.
/// For <see cref="System.Guid"/> the generation strategy is implied; for <see cref="int"/> and
/// <see cref="long"/> the id is treated as externally assigned (database identity).
/// </summary>
/// <typeparam name="TValue">Underlying primitive: <see cref="System.Guid"/>, <see cref="int"/>,
/// <see cref="long"/>, or <see cref="string"/>.</typeparam>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue> : Attribute;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a strongly-typed id backed by <typeparamref name="TValue"/>
/// and generated using <typeparamref name="TStrategy"/>.
/// </summary>
/// <typeparam name="TValue">Underlying primitive type.</typeparam>
/// <typeparam name="TStrategy">Generation strategy: <see cref="Snowflake"/>, <see cref="Ulid"/>,
/// <see cref="NanoId"/>, <see cref="GuidV7"/>, <see cref="Cuid2"/>, <see cref="Ksuid"/>,
/// <see cref="ObjectId"/>, or <see cref="SequentialGuid"/>.</typeparam>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class OrionIdAttribute<TValue, TStrategy> : Attribute;
