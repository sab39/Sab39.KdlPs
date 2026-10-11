using KdlSharp;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>Builds Get-KdlNode's output from a KdlSharp node.</summary>
internal sealed class KdlNodeBuilder(Action<string> warn)
{
    private readonly KdlValueConverter converter = new(warn);

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
        var arguments = node.Arguments.Select(a => (Key: (string?)null, Value: a));
        var properties = node.Properties.Select(p => (Key: (string?)p.Key, p.Value));
        var valueElements = from element in arguments.Concat(properties)
                            orderby element.Value.SourcePosition!.Offset
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
}
