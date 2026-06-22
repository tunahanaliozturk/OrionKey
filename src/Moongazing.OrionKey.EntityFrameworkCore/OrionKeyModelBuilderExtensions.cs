namespace Moongazing.OrionKey.EntityFrameworkCore;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Model-wide registration of OrionKey value converters. One call wires the converter for every
/// <c>[OrionId]</c> property discovered on the model, so consumers stop calling
/// <c>HasOrionKeyConversion()</c> property by property.
/// </summary>
/// <remarks>
/// This is the headline ergonomics helper: <see cref="UseOrionKeyConversions(ModelBuilder)"/> walks
/// the model and applies the converter to each OrionKey id property. It pairs with the per-property
/// <see cref="OrionKeyPropertyBuilderExtensions.HasOrionKeyConversion{TId, TValue}(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder{TId})"/>
/// helper (and the one the generator emits in each id's namespace) for the cases that need explicit
/// per-property control.
/// </remarks>
public static class OrionKeyModelBuilderExtensions
{
    /// <summary>
    /// Discover every OrionKey id property on the model and wire its value converter. Call once, at the
    /// end of <c>OnModelCreating</c>, after the entity types are configured.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modelBuilder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A property is recognized as an OrionKey id by the <c>OrionIdAttribute</c> on its type. An id
    /// property the provider has not already mapped (the bare struct is not a scalar the provider
    /// understands) is added to the entity so the conversion takes effect; an already-mapped property
    /// has its converter set in place. Properties that already carry a value converter are left
    /// untouched, so an explicit per-property configuration always wins over this convention.
    /// </remarks>
    [RequiresUnreferencedCode(OrionKeyValueConverterFactory.ConventionTrimWarning)]
    [RequiresDynamicCode(OrionKeyValueConverterFactory.ConventionAotWarning)]
    public static ModelBuilder UseOrionKeyConversions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            ApplyToEntity(entityType);
        }

        return modelBuilder;
    }

    [RequiresUnreferencedCode(OrionKeyValueConverterFactory.ConventionTrimWarning)]
    [RequiresDynamicCode(OrionKeyValueConverterFactory.ConventionAotWarning)]
    private static void ApplyToEntity(IMutableEntityType entityType)
    {
        // Snapshot the CLR properties up front: the loop may call AddProperty, which mutates the
        // entity's property collection. The CLR property list is the stable source of candidates,
        // matching the EF Core bulk-configuration guidance for value types the provider does not map
        // on its own.
        var clrProperties = GetMappableProperties(entityType.ClrType);

        foreach (var clrProperty in clrProperties)
        {
            if (!OrionKeyMetadata.HasOrionIdAttribute(clrProperty.PropertyType))
            {
                continue;
            }

            var existing = entityType.FindProperty(clrProperty.Name);

            // Leave a property that is already converted alone: explicit configuration, or a second
            // pass of this convention, must not clobber an existing converter.
            if (existing is not null && existing.GetValueConverter() is not null)
            {
                continue;
            }

            var property = existing ?? entityType.AddProperty(clrProperty);
            property.SetValueConverter(OrionKeyValueConverterFactory.Create(clrProperty.PropertyType));
        }
    }

    [RequiresUnreferencedCode("Reflects over the entity CLR type's public instance properties.")]
    private static PropertyInfo[] GetMappableProperties(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type clrType)
        => clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
}
