namespace Moongazing.OrionKey.EntityFrameworkCore;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Recognizes OrionKey id structs and resolves their underlying primitive, from runtime metadata.
/// </summary>
/// <remarks>
/// OrionKey ids carry no marker interface: a generated id implements only <c>IEquatable&lt;T&gt;</c>,
/// <c>IFormattable</c>, <c>ISpanFormattable</c> (and the parse interfaces), none of which is unique
/// to OrionKey. The reliable runtime marker is the <see cref="OrionIdAttribute{TValue}"/> /
/// <see cref="OrionIdAttribute{TValue, TStrategy}"/> the consumer places on the struct, matched here
/// by open-generic type definition so both arities are recognized.
/// </remarks>
internal static class OrionKeyMetadata
{
    /// <summary>
    /// Determine whether <paramref name="type"/> is an OrionKey id struct and, if so, the type of its
    /// underlying <c>Value</c> primitive.
    /// </summary>
    /// <param name="type">Candidate CLR type (an entity property's type).</param>
    /// <param name="valueType">The underlying primitive when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is an OrionKey id.</returns>
    [RequiresUnreferencedCode(
        "Resolving the OrionKey id Value property requires reflecting over the id type's members.")]
    internal static bool TryGetIdValueType(
        Type type,
        [NotNullWhen(true)] out Type? valueType)
    {
        valueType = null;

        if (type is null || !type.IsValueType)
        {
            return false;
        }

        if (!HasOrionIdAttribute(type))
        {
            return false;
        }

        // The Value property is the underlying primitive. Resolving it here, rather than reading the
        // attribute's generic argument, keeps a single source of truth and tolerates the id being a
        // closed type whose attribute reflection is awkward to read across runtimes.
        var valueProperty = GetValueProperty(type);
        if (valueProperty is null)
        {
            return false;
        }

        valueType = valueProperty.PropertyType;
        return true;
    }

    /// <summary>True when <paramref name="type"/> carries an <c>OrionIdAttribute&lt;...&gt;</c> of either arity.</summary>
    internal static bool HasOrionIdAttribute(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        foreach (var attribute in type.GetCustomAttributes(inherit: false))
        {
            var attributeType = attribute.GetType();
            if (attributeType.IsGenericType)
            {
                var definition = attributeType.GetGenericTypeDefinition();
                if (definition == typeof(OrionIdAttribute<>) || definition == typeof(OrionIdAttribute<,>))
                {
                    return true;
                }
            }
        }

        return false;
    }

    [RequiresUnreferencedCode("Reflects over the id type's public instance properties.")]
    private static PropertyInfo? GetValueProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        => type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
}
