namespace Moongazing.OrionKey.EntityFrameworkCore;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Pre-convention (<c>ConfigureConventions</c>) registration of OrionKey value converters, the
/// counterpart to <see cref="OrionKeyModelBuilderExtensions.UseOrionKeyConversions(ModelBuilder)"/>
/// for consumers who prefer to configure conventions rather than walk the built model.
/// </summary>
/// <remarks>
/// <para>
/// Pre-convention configuration is keyed by CLR type and applied as each matching property is
/// discovered, so it cannot take a converter instance: EF Core's
/// <c>Properties(Type).HaveConversion(Type)</c> instantiates a converter <em>type</em> that must have
/// a public parameterless constructor. The generator emits exactly such a type per id,
/// <c>{Name}ValueConverter</c>, in the id's own namespace. This helper locates that generated
/// converter for every <c>[OrionId]</c> type in the supplied assemblies and registers it once.
/// </para>
/// <para>
/// This path is reflective by nature (it scans assemblies and resolves the generated converter type
/// by name) and is therefore not trimming- or AOT-safe. Under Native AOT, prefer either the explicit
/// generated overload per id, <c>configurationBuilder.Properties&lt;UserId&gt;().HaveConversion&lt;UserIdValueConverter&gt;()</c>,
/// or the strongly-typed per-property
/// <see cref="OrionKeyPropertyBuilderExtensions.HasOrionKeyConversion{TId, TValue}(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder{TId})"/>.
/// </para>
/// </remarks>
public static class OrionKeyConventionExtensions
{
    /// <summary>
    /// Register the generated value converter for every <c>[OrionId]</c> type declared in
    /// <paramref name="idAssemblies"/> (defaulting to the calling assembly when none are supplied), via
    /// pre-convention configuration.
    /// </summary>
    /// <param name="configurationBuilder">The convention configuration builder from <c>ConfigureConventions</c>.</param>
    /// <param name="idAssemblies">
    /// Assemblies to scan for <c>[OrionId]</c> structs. When empty, the calling assembly is scanned.
    /// </param>
    /// <returns>The same <paramref name="configurationBuilder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configurationBuilder"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode(
        "Scans assemblies for [OrionId] types and resolves their generated converter by name; not trimming-safe.")]
    [RequiresDynamicCode(
        "Resolves and registers generated converter types discovered at runtime; not AOT-safe.")]
    public static ModelConfigurationBuilder ConfigureOrionKeyConversions(
        this ModelConfigurationBuilder configurationBuilder,
        params Assembly[] idAssemblies)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        var assemblies = idAssemblies is { Length: > 0 }
            ? idAssemblies
            : [Assembly.GetCallingAssembly()];

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var idType in assembly.GetTypes())
            {
                if (!idType.IsValueType || !OrionKeyMetadata.HasOrionIdAttribute(idType))
                {
                    continue;
                }

                var converterType = ResolveGeneratedConverter(idType);
                if (converterType is not null)
                {
                    configurationBuilder.Properties(idType).HaveConversion(converterType);
                }
            }
        }

        return configurationBuilder;
    }

    /// <summary>
    /// Resolve the generator-emitted <c>{Name}ValueConverter</c> for an OrionKey id, which lives next
    /// to the id type and derives from <see cref="ValueConverter"/> with a public parameterless ctor.
    /// </summary>
    [RequiresUnreferencedCode("Resolves the generated converter type by name from the id's assembly.")]
    private static Type? ResolveGeneratedConverter(Type idType)
    {
        var converterName = idType.Namespace is { Length: > 0 } ns
            ? $"{ns}.{idType.Name}ValueConverter"
            : $"{idType.Name}ValueConverter";

        var converterType = idType.Assembly.GetType(converterName, throwOnError: false);

        return converterType is not null && typeof(ValueConverter).IsAssignableFrom(converterType)
            ? converterType
            : null;
    }
}
