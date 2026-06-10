using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Moongazing.OrionKey.Generators;

/// <summary>
/// An <see cref="ImmutableArray{T}"/> wrapper that compares by element-equality so the
/// Roslyn incremental pipeline can cache it. <see cref="ImmutableArray{T}"/> itself
/// compares by reference identity, which busts the cache on every recompile.
/// </summary>
/// <remarks>
/// Pattern used widely in the dotnet ecosystem (StronglyTypedId, MediatR, Mediator,
/// Andrew Lock's blog series). The whole point of incremental generators is that
/// equal inputs reuse cached outputs; without value-equality on collections, the cache
/// is permanently cold for any step that returns an array.
/// </remarks>
internal readonly struct EquatableArray<T>
    : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> array;

    public EquatableArray(ImmutableArray<T> array) => this.array = array;

    public T this[int index] => array[index];

    public int Count => array.IsDefault ? 0 : array.Length;

    public bool IsEmpty => Count == 0;

    public ImmutableArray<T> AsImmutableArray() => array.IsDefault ? ImmutableArray<T>.Empty : array;

    public IEnumerator<T> GetEnumerator()
    {
        if (array.IsDefault)
        {
            yield break;
        }
        foreach (var item in array)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        var a = array.IsDefault ? ImmutableArray<T>.Empty : array;
        var b = other.array.IsDefault ? ImmutableArray<T>.Empty : other.array;
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (array.IsDefaultOrEmpty)
        {
            return 0;
        }
        unchecked
        {
            var hash = 17;
            foreach (var item in array)
            {
                hash = (hash * 31) + (item is null ? 0 : item.GetHashCode());
            }
            return hash;
        }
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

internal static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this ImmutableArray<T> array) where T : IEquatable<T>
        => new(array);

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
        => new(ImmutableArray.CreateRange(source));
}
