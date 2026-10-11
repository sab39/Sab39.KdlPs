using System.Management.Automation;
using System.Numerics;

using KdlSharp;
using KdlSharp.Values;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// Turns this module's nodes back into KdlSharp nodes, for KdlSharp to serialize. Values of types it
/// can't write throw NotSupportedException.
/// </summary>
internal static class KdlSharpConverter
{
    /// <summary>Normalize sorts properties by key, ordinally, as the official test suite's expected files do.</summary>
    public static KdlSharpNode ToKdlSharp(KdlNode node, bool normalize)
    {
        var annotation = node.RawNodeElements.FirstOrDefault(e => e.Kind == KdlElementKind.NodeName)?.Annotation;
        KdlSharpNode result = annotation is null ? new(node.NodeName) : new(node.NodeName, new KdlAnnotation(annotation));

        foreach (var argument in node.NodeArguments) result.AddArgument(ToKdlValue(argument));

        var properties = normalize ? node.NodeProperties.OrderBy(p => p.Key, StringComparer.Ordinal) : node.NodeProperties.AsEnumerable();
        foreach (var (key, value) in properties) result.AddProperty(key, ToKdlValue(value));

        foreach (var child in node.ChildNodes) result.AddChild(ToKdlSharp(child, normalize));

        return result;
    }

    private static KdlValue ToKdlValue(object? value)
        => value switch
        {
            null => KdlNull.Instance,
            PSObject wrapped => ToKdlValue(wrapped.BaseObject),
            bool b => b ? KdlBoolean.True : KdlBoolean.False,
            string s => new KdlString(s),

            sbyte n => new KdlNumber(n),
            byte n => new KdlNumber(n),
            short n => new KdlNumber(n),
            ushort n => new KdlNumber(n),
            int n => new KdlNumber(n),
            uint n => new KdlNumber(n),
            long n => new KdlNumber(n),
            ulong n => new KdlNumber(n),
            nint n => new KdlNumber(n),
            nuint n => new KdlNumber(n),
            Int128 n => new KdlNumber((BigInteger)n),
            UInt128 n => new KdlNumber((BigInteger)n),
            BigInteger n => new KdlNumber(n),
            decimal n => new KdlNumber(n),
            float n => new KdlNumber(n),
            double n => new KdlNumber(n),
            _ => throw new NotSupportedException($"Values of type {value.GetType()} can't be written yet."),
        };
}
