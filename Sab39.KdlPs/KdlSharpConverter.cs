using System.Management.Automation;
using System.Numerics;

using KdlSharp;
using KdlSharp.Values;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// Builds KdlSharp nodes from .NET values, for nodes made by New-KdlNode. Values of types that can't be
/// written throw NotSupportedException.
/// </summary>
internal static class KdlSharpConverter
{
    public static KdlSharpNode ToKdlSharp(
        string nodeName,
        IEnumerable<object?> arguments,
        IEnumerable<KeyValuePair<string, object?>> properties,
        IEnumerable<KdlNode> childNodes)
    {
        KdlSharpNode result = new(nodeName);
        foreach (var argument in arguments) result.AddArgument(ToKdlValue(argument));
        foreach (var (key, value) in properties) result.AddProperty(key, ToKdlValue(value));

        // Cloned, since a KdlSharp node can only have one parent
        foreach (var child in childNodes) result.AddChild(child.Backing.Clone());

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
