using KdlSharp;

namespace Sab39.KdlPs;

/// <summary>
/// One element of a KDL node as written: its name, an argument, or a property.
/// </summary>
public sealed class KdlElement(KdlElementKind kind, string? key, object? value, string? annotation, SourcePosition? sourcePosition)
{
    public KdlElementKind Kind { get; } = kind;

    /// <summary>The property key, or null for anything that isn't a property.</summary>
    public string? Key { get; } = key;

    public object? Value { get; } = value;

    public string? Annotation { get; } = annotation;

    /// <summary>Where the value starts, after any key or annotation.</summary>
    public SourcePosition? SourcePosition { get; } = sourcePosition;

    public override string ToString() => Key is null ? $"{Value}" : $"{Key}={Value}";
}
