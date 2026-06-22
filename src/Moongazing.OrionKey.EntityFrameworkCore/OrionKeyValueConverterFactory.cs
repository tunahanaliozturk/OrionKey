namespace Moongazing.OrionKey.EntityFrameworkCore;

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Builds an EF Core <see cref="ValueConverter"/> that maps an OrionKey id struct to and from
/// its underlying primitive (<see cref="System.Guid"/>, <see cref="int"/>, <see cref="long"/>,
/// or <see cref="string"/>).
/// </summary>
/// <remarks>
/// <para>
/// Every <c>[OrionId]</c> struct exposes a public <c>Value</c> getter and a public
/// <c>(TValue value)</c> constructor (both emitted by the OrionKey source generator), so the
/// conversion is the pair <c>id =&gt; id.Value</c> and <c>value =&gt; new TId(value)</c>. This is
/// the same conversion the generator's per-id <c>{Name}ValueConverter</c> hard-codes; rebuilding
/// it here lets a single model-wide convention cover every id without referencing the per-id
/// converter type, which lives in the id's own namespace.
/// </para>
/// <para>
/// Two construction paths exist, mirroring how the System.Text.Json registrar is shaped. The
/// generic <see cref="Create{TId, TValue}"/> is the reflection-free, AOT- and trimming-safe path:
/// the id type and its members are statically reachable from the call site, and the
/// <see cref="DynamicallyAccessedMembersAttribute"/> annotation preserves the <c>Value</c> getter
/// and the constructor through trimming. The non-generic <see cref="Create(Type)"/> path drives
/// the model scan and is therefore annotated as requiring unreferenced code and dynamic code: it
/// resolves the same members from a runtime <see cref="Type"/>.
/// </para>
/// </remarks>
public static class OrionKeyValueConverterFactory
{
    /// <summary>
    /// Member types the runtime needs preserved on an OrionKey id for value conversion: the public
    /// <c>Value</c> property getter and the public <c>(TValue)</c> constructor.
    /// </summary>
    internal const DynamicallyAccessedMemberTypes RequiredMembers =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicConstructors;

    /// <summary>
    /// Create a strongly-typed <see cref="ValueConverter{TModel, TProvider}"/> for the OrionKey id
    /// <typeparamref name="TId"/> backed by <typeparamref name="TValue"/>. Reflection-free and safe
    /// under Native AOT and trimming.
    /// </summary>
    /// <typeparam name="TId">The OrionKey id struct type.</typeparam>
    /// <typeparam name="TValue">The underlying primitive: Guid, int, long, or string.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TId"/> does not have the <c>Value</c> getter / <c>(TValue)</c> constructor
    /// shape of an OrionKey id.
    /// </exception>
    public static ValueConverter<TId, TValue> Create<
        [DynamicallyAccessedMembers(RequiredMembers)] TId, TValue>()
        where TId : struct
    {
        // Resolve the members through the annotated type so the trimmer preserves them, then feed the
        // PropertyInfo / ConstructorInfo into the non-string Expression overloads. The string-keyed
        // Expression.Property(expr, "Value") is itself annotated RequiresUnreferencedCode and would
        // make even this statically-reachable path trim-unsafe.
        var valueProperty = typeof(TId).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"OrionKey id '{typeof(TId)}' has no public 'Value' property; " +
                "it does not look like an OrionKey id.");

        var ctor = typeof(TId).GetConstructor([typeof(TValue)])
            ?? throw new InvalidOperationException(
                $"OrionKey id '{typeof(TId)}' has no public constructor accepting '{typeof(TValue)}'; " +
                "it does not look like an OrionKey id backed by that type.");

        // id => id.Value
        var idParam = Expression.Parameter(typeof(TId), "id");
        var toProvider = Expression.Lambda<Func<TId, TValue>>(
            Expression.Property(idParam, valueProperty),
            idParam);

        // value => new TId(value)
        var valueParam = Expression.Parameter(typeof(TValue), "value");
        var fromProvider = Expression.Lambda<Func<TValue, TId>>(
            Expression.New(ctor, valueParam),
            valueParam);

        return new ValueConverter<TId, TValue>(toProvider, fromProvider);
    }

    /// <summary>
    /// Create a <see cref="ValueConverter"/> for the OrionKey id <paramref name="idType"/>, resolving
    /// its underlying primitive from the <c>Value</c> property. Used by the model-wide convention,
    /// which discovers id types at runtime.
    /// </summary>
    /// <param name="idType">An OrionKey id struct type (carries <c>OrionIdAttribute</c>).</param>
    /// <returns>A converter mapping <paramref name="idType"/> to and from its underlying primitive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="idType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="idType"/> does not have the shape of an OrionKey id.</exception>
    [RequiresUnreferencedCode(ConventionTrimWarning)]
    [RequiresDynamicCode(ConventionAotWarning)]
    public static ValueConverter Create(
        [DynamicallyAccessedMembers(RequiredMembers)] Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        var valueProperty = idType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new ArgumentException(
                $"Type '{idType}' has no public 'Value' property; it does not look like an OrionKey id.",
                nameof(idType));

        var valueType = valueProperty.PropertyType;

        var ctor = idType.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance, binder: null, [valueType], modifiers: null)
            ?? throw new ArgumentException(
                $"Type '{idType}' has no public constructor accepting '{valueType}'; it does not look like an OrionKey id.",
                nameof(idType));

        // id => id.Value
        var idParam = Expression.Parameter(idType, "id");
        var toProvider = Expression.Lambda(
            Expression.Property(idParam, valueProperty),
            idParam);

        // value => new TId(value)
        var valueParam = Expression.Parameter(valueType, "value");
        var fromProvider = Expression.Lambda(
            Expression.New(ctor, valueParam),
            valueParam);

        var converterType = typeof(ValueConverter<,>).MakeGenericType(idType, valueType);
        return (ValueConverter)Activator.CreateInstance(
            converterType,
            toProvider,
            fromProvider,
            /* mappingHints */ null)!;
    }

    internal const string ConventionTrimWarning =
        "The OrionKey EF Core model-wide convention discovers id types and their Value member by " +
        "reflection over the model. Use the generic HasOrionKeyConversion<TId, TValue>() helper for " +
        "a fully trimming-safe registration.";

    internal const string ConventionAotWarning =
        "The OrionKey EF Core model-wide convention builds value converters with " +
        "Activator.CreateInstance over a runtime-constructed generic type. Use the generic " +
        "HasOrionKeyConversion<TId, TValue>() helper under Native AOT.";
}
