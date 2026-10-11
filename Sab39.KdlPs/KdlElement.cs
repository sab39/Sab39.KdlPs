using KdlSharp;

namespace Sab39.KdlPs;

/// <summary>
/// One element of a KDL node as written: its name, an argument, or a property. The source text members
/// are only set for elements that were read from KDL.
/// </summary>
public sealed class KdlElement(
    KdlElementKind kind,
    string? key,
    object? value,
    string? annotation,
    SourcePosition? sourcePosition,
    SourceText? sourceText = null,
    SourceText? keySourceText = null,
    SourceText? annotationSourceText = null,
    SourceText? valueSourceText = null)
{
    public KdlElementKind Kind { get; } = kind;

    /// <summary>The property key, or null for anything that isn't a property.</summary>
    public string? Key { get; } = key;

    public object? Value { get; } = value;

    public string? Annotation { get; } = annotation;

    /// <summary>Where the value starts, after any key or annotation.</summary>
    public SourcePosition? SourcePosition { get; } = sourcePosition;

    /// <summary>The whole element as written: from the key or annotation, if there is one, to the end of the value.</summary>
    public SourceText? SourceText { get; } = sourceText;

    /// <summary>The property key as written.</summary>
    public SourceText? KeySourceText { get; } = keySourceText;

    /// <summary>The annotation as written, parentheses included.</summary>
    public SourceText? AnnotationSourceText { get; } = annotationSourceText;

    /// <summary>The value, or the node name, as written.</summary>
    public SourceText? ValueSourceText { get; } = valueSourceText;

    public override string ToString() => Key is null ? $"{Value}" : $"{Key}={Value}";
}
