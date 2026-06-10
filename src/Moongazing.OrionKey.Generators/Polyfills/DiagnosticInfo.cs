using System;
using Microsoft.CodeAnalysis;

namespace Moongazing.OrionKey.Generators;

/// <summary>
/// Cache-friendly representation of a <see cref="Diagnostic"/> for use in the incremental
/// pipeline. <see cref="Diagnostic"/> instances are not value-equal; tunnelling them
/// through the pipeline busts the cache. <see cref="DiagnosticInfo"/> is a value record
/// of the descriptor id + a serialised <see cref="Location"/> hash; the generator
/// re-creates a <see cref="Diagnostic"/> at output time.
/// </summary>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(
        Descriptor,
        Location.ToLocation(),
        ToObjectArray(MessageArgs));

    public static DiagnosticInfo From(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
        => new(descriptor, LocationInfo.From(location), messageArgs.ToEquatableArray());

    private static object?[] ToObjectArray(EquatableArray<string> args)
    {
        if (args.IsEmpty)
        {
            return Array.Empty<object?>();
        }
        var arr = new object?[args.Count];
        for (var i = 0; i < args.Count; i++)
        {
            arr[i] = args[i];
        }
        return arr;
    }
}

/// <summary>
/// Cache-friendly representation of a <see cref="Location"/>. Captures the source path
/// + textual span so it can be reconstructed without holding onto the Roslyn syntax tree.
/// </summary>
internal readonly record struct LocationInfo(
    string? FilePath,
    TextSpanInfo Span,
    LinePositionSpanInfo LineSpan)
{
    public Location ToLocation()
    {
        if (FilePath is null)
        {
            return Location.None;
        }
        return Location.Create(
            FilePath,
            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(Span.Start, Span.End),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.StartLine, LineSpan.StartCharacter),
                new Microsoft.CodeAnalysis.Text.LinePosition(LineSpan.EndLine, LineSpan.EndCharacter)));
    }

    public static LocationInfo From(Location? location)
    {
        if (location is null || location == Location.None)
        {
            return new LocationInfo(null, default, default);
        }
        var span = location.GetLineSpan();
        return new LocationInfo(
            location.SourceTree?.FilePath ?? span.Path,
            new TextSpanInfo(location.SourceSpan.Start, location.SourceSpan.End),
            new LinePositionSpanInfo(
                span.StartLinePosition.Line, span.StartLinePosition.Character,
                span.EndLinePosition.Line, span.EndLinePosition.Character));
    }
}

internal readonly record struct TextSpanInfo(int Start, int End);

internal readonly record struct LinePositionSpanInfo(int StartLine, int StartCharacter, int EndLine, int EndCharacter);
