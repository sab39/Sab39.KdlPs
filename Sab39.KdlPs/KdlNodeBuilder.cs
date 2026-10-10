using KdlSharp;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// Builds Get-KdlNode's output from a KdlSharp node. Source is the text the node was parsed from,
/// which numbers are re-read out of.
/// </summary>
internal sealed class KdlNodeBuilder(string source, Action<string> warn)
{
    private readonly KdlValueConverter converter = new(source, warn);

    public KdlNode Build(KdlSharpNode node)
    {
        var nameAnnotation = node.TypeAnnotation?.TypeName;
        KdlElement nameElement = new(
            KdlElementKind.NodeName,
            null,
            KdlValueConverter.ConvertNodeName(node.Name, nameAnnotation),
            nameAnnotation,
            node.SourcePosition);

        // KdlSharp keeps arguments and properties apart, so their interleaving comes back from where
        // each one starts. Converting in that order also puts any warnings in source order.
        var arguments = WithEffectiveOffsets(node.Arguments.Select(a => (Key: (string?)null, Value: a)));
        var properties = WithEffectiveOffsets(node.Properties.Select(p => (Key: (string?)p.Key, p.Value)));
        var valueElements = from element in arguments.Concat(properties)
                            orderby element.Offset
                            select new KdlElement(
                                element.Key is null ? KdlElementKind.Argument : KdlElementKind.Property,
                                element.Key,
                                this.converter.ConvertValue(element.Value),
                                element.Value.TypeAnnotation?.TypeName,
                                element.Value.SourcePosition);

        KdlElement[] elements = [nameElement, .. valueElements];

        OrderedDictionary<string, object?> propertyValues = new(StringComparer.Ordinal);
        foreach (var property in elements.Where(e => e.Kind == KdlElementKind.Property)) propertyValues[property.Key!] = property.Value;

        return new(
            node.Name,
            [.. elements.Where(e => e.Kind == KdlElementKind.Argument).Select(e => e.Value)],
            propertyValues,
            [.. node.Children.Select(this.Build)],
            elements);
    }

    /// <summary>
    /// KdlSharp gives #true, #false and #null no source position, so each of those takes the offset of
    /// whatever precedes it in its own list. A stable sort then keeps arguments in order among
    /// themselves, and properties too - but a keyword value can land on the wrong side of an element
    /// from the other list.
    /// </summary>
    private static IEnumerable<(string? Key, KdlValue Value, int Offset)> WithEffectiveOffsets(IEnumerable<(string? Key, KdlValue Value)> elements)
    {
        var offset = -1;
        foreach (var (key, value) in elements)
        {
            offset = value.SourcePosition?.Offset ?? offset;
            yield return (key, value, offset);
        }
    }
}
