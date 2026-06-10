namespace Moongazing.OrionKey.Aot;

using System.Collections.Concurrent;

/// <summary>
/// Process-wide registry that maps OrionId struct types to their parse / format delegates
/// WITHOUT reflection at the dispatch site. Designed for framework authors (ASP.NET
/// model binders, route value converters, MVC content-type-aware deserializers) who need
/// to convert <c>string</c> &lt;-&gt; ID-value across many OrionId types in a single
/// method, in a NativeAOT-friendly way: consumers register each ID type at startup with
/// strongly-typed delegates, then <see cref="TryParse"/> and <see cref="Format"/> dispatch
/// via a <see cref="ConcurrentDictionary{TKey, TValue}"/> lookup keyed by <see cref="Type"/>.
/// </summary>
/// <remarks>
/// <para>
/// The alternative - <c>typeof(IdType).GetMethod("Parse")</c> at request time - requires
/// reflection-based metadata, which the trim/AOT analyzer warns about and which is the
/// largest source of trim warnings in framework code that wants to be ID-agnostic. The
/// registry shifts that decision back to startup time, where consumers know which IDs they
/// use and can register strongly-typed delegates the generator already emits (via the
/// per-struct <c>IParsable&lt;T&gt;.Parse</c> and <c>ToString</c>).
/// </para>
/// <para>
/// The registry is process-global and append-only; calling <see cref="Register{TId}"/>
/// twice for the same type is idempotent (the first registration wins). Consumers
/// typically call <see cref="Register{TId}"/> from a one-time bootstrap method, e.g.:
/// <code>
/// OrionKeyTypeRegistry.Register&lt;UserId&gt;(s =&gt; UserId.Parse(s), id =&gt; id.ToString());
/// OrionKeyTypeRegistry.Register&lt;OrderId&gt;(s =&gt; OrderId.Parse(s), id =&gt; id.ToString());
/// </code>
/// </para>
/// </remarks>
public static class OrionKeyTypeRegistry
{
    private static readonly ConcurrentDictionary<Type, ParseAndFormat> Entries = new();

    /// <summary>
    /// Register a parse + format delegate pair for <typeparamref name="TId"/>. Idempotent;
    /// the first registration for a given type wins and subsequent calls are no-ops.
    /// </summary>
    /// <typeparam name="TId">The OrionId struct type.</typeparam>
    /// <param name="parse">Parses a string into a <typeparamref name="TId"/>. Typically <c>TId.Parse</c>.</param>
    /// <param name="format">Renders a <typeparamref name="TId"/> to string. Typically <c>id =&gt; id.ToString()</c>.</param>
    /// <returns><see langword="true"/> when this call performed the registration; <see langword="false"/> when an earlier call already won.</returns>
    public static bool Register<TId>(Func<string, TId> parse, Func<TId, string> format)
        where TId : struct
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(format);
        return Entries.TryAdd(typeof(TId),
            new ParseAndFormat(
                ParseBoxed: s => parse(s),
                FormatBoxed: o => format((TId)o)));
    }

    /// <summary>
    /// Dispatch parsing of <paramref name="value"/> into a registered ID type. Returns
    /// <see langword="false"/> when <paramref name="idType"/> has not been registered;
    /// otherwise the parse delegate's result is boxed into <paramref name="id"/>.
    /// </summary>
    /// <param name="idType">CLR type of the ID struct.</param>
    /// <param name="value">Input string.</param>
    /// <param name="id">Boxed ID on success; <see langword="null"/> on failure.</param>
    public static bool TryParse(Type idType, string value, out object? id)
    {
        ArgumentNullException.ThrowIfNull(idType);
        ArgumentNullException.ThrowIfNull(value);
        if (!Entries.TryGetValue(idType, out var entry))
        {
            id = null;
            return false;
        }
        try
        {
            id = entry.ParseBoxed(value);
            return id is not null;
        }
        catch (FormatException)
        {
            id = null;
            return false;
        }
        catch (ArgumentException)
        {
            id = null;
            return false;
        }
        catch (OverflowException)
        {
            id = null;
            return false;
        }
    }

    /// <summary>
    /// Dispatch formatting of a boxed <paramref name="id"/> via the registered delegate for
    /// <paramref name="idType"/>. Throws when <paramref name="idType"/> has not been
    /// registered or when <paramref name="id"/> is not an instance of
    /// <paramref name="idType"/>.
    /// </summary>
    public static string Format(Type idType, object id)
    {
        ArgumentNullException.ThrowIfNull(idType);
        ArgumentNullException.ThrowIfNull(id);
        if (!idType.IsInstanceOfType(id))
        {
            throw new ArgumentException(
                $"OrionKeyTypeRegistry.Format: instance type '{id.GetType()}' is not '{idType}'.", nameof(id));
        }
        if (!Entries.TryGetValue(idType, out var entry))
        {
            throw new InvalidOperationException(
                $"OrionKeyTypeRegistry.Format: no entry registered for type '{idType}'. " +
                "Call OrionKeyTypeRegistry.Register<T>(parse, format) at startup.");
        }
        return entry.FormatBoxed(id);
    }

    /// <summary>True when the type has been registered.</summary>
    public static bool IsRegistered(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);
        return Entries.ContainsKey(idType);
    }

    /// <summary>Snapshot of all registered ID types. The returned array is decoupled from internal state.</summary>
    public static Type[] RegisteredTypes() => Entries.Keys.ToArray();

    /// <summary>Test-only helper: clears all registered entries.</summary>
    internal static void ResetForTests() => Entries.Clear();

    private readonly record struct ParseAndFormat(
        Func<string, object?> ParseBoxed,
        Func<object, string> FormatBoxed);
}
