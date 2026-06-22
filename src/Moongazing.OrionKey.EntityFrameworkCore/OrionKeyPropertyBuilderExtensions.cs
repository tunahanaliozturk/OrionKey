namespace Moongazing.OrionKey.EntityFrameworkCore;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Per-property fluent helpers for wiring the OrionKey value converter, provider-agnostic and usable
/// without importing the id's namespace.
/// </summary>
/// <remarks>
/// The OrionKey source generator already emits a <c>HasOrionKeyConversion()</c> extension in each
/// id's own namespace (since v0.5.10). This generic overload complements it: it lives in
/// <c>Moongazing.OrionKey.EntityFrameworkCore</c>, so a configuration file that converts several ids
/// needs a single <c>using</c>, and it is the reflection-free building block the model-wide
/// convention falls back to when you want a fully trimming- and AOT-safe registration.
/// </remarks>
public static class OrionKeyPropertyBuilderExtensions
{
    /// <summary>
    /// Wire the OrionKey value converter on a <see cref="PropertyBuilder{TId}"/> for the id
    /// <typeparamref name="TId"/> backed by <typeparamref name="TValue"/>. Reflection-free; safe under
    /// Native AOT and trimming.
    /// </summary>
    /// <typeparam name="TId">The OrionKey id struct type.</typeparam>
    /// <typeparam name="TValue">The underlying primitive: Guid, int, long, or string.</typeparam>
    /// <param name="builder">The property builder for the id property.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static PropertyBuilder<TId> HasOrionKeyConversion<
        [DynamicallyAccessedMembers(OrionKeyValueConverterFactory.RequiredMembers)] TId, TValue>(
        this PropertyBuilder<TId> builder)
        where TId : struct
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.HasConversion(OrionKeyValueConverterFactory.Create<TId, TValue>());
    }
}
