using System.Globalization;
using System.Management.Automation;
using System.Numerics;

using KdlSharp;
using KdlSharp.Values;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// Turns this module's nodes back into KdlSharp nodes, for KdlSharp to serialize. Anything KdlSharp
/// can't yet represent or write correctly throws NotSupportedException rather than being written wrong.
/// </summary>
internal static class KdlSharpConverter
{
    /// <summary>Normalize sorts properties by key, ordinally, as the official test suite's expected files do.</summary>
    public static KdlSharpNode ToKdlSharp(KdlNode node, bool normalize)
    {
        CheckName(node.NodeName, "node name");
        var annotation = node.RawNodeElements.FirstOrDefault(e => e.Kind == KdlElementKind.NodeName)?.Annotation;
        KdlSharpNode result = annotation is null ? new(node.NodeName) : new(node.NodeName, new KdlAnnotation(annotation));

        foreach (var argument in node.NodeArguments) result.AddArgument(ToKdlValue(argument));

        var properties = normalize ? node.NodeProperties.OrderBy(p => p.Key, StringComparer.Ordinal) : node.NodeProperties.AsEnumerable();
        foreach (var (key, value) in properties)
        {
            CheckName(key, "property key");
            result.AddProperty(key, ToKdlValue(value));
        }

        foreach (var child in node.ChildNodes) result.AddChild(ToKdlSharp(child, normalize));

        return result;
    }

    private static KdlValue ToKdlValue(object? value)
        => value switch
        {
            null => KdlNull.Instance,
            PSObject wrapped => ToKdlValue(wrapped.BaseObject),
            bool b => b ? KdlBoolean.True : KdlBoolean.False,

            // KdlSharp writes a bare string without checking whether it would read back as a number.
            string s => new KdlString(s, CouldReadAsNumber(s) ? KdlStringType.Quoted : KdlStringType.Identifier),

            sbyte n => FromInteger(n),
            byte n => FromInteger(n),
            short n => FromInteger(n),
            ushort n => FromInteger(n),
            int n => FromInteger(n),
            uint n => FromInteger(n),
            long n => FromInteger(n),
            ulong n => FromInteger(n),
            nint n => FromInteger(n),
            nuint n => FromInteger(n),
            Int128 n => FromInteger(n),
            UInt128 n => FromInteger(n),
            BigInteger n => FromInteger(n),
            decimal n => new KdlNumber(n, KdlNumberFormat.Decimal),
            float n => FromDouble(n),
            double n => FromDouble(n),
            _ => throw new NotSupportedException($"Values of type {value.GetType()} can't be written yet."),
        };

    /// <summary>KdlSharp holds every number as a decimal, so integers beyond its range can't be written yet.</summary>
    private static KdlNumber FromInteger<T>(T value)
        where T : IBinaryInteger<T>
    {
        try
        {
            return new(decimal.CreateChecked(value), KdlNumberFormat.Decimal);
        }
        catch (OverflowException)
        {
            throw new NotSupportedException($"{value} is too large to be written yet.");
        }
    }

    /// <summary>
    /// Goes through the round-trip string form, since a direct cast to decimal keeps only about 15
    /// significant digits. Values beyond decimal's range, or too small for it, can't be written yet.
    /// A whole number gets a scale of one, so that it's written as 1.0 rather than reading back as an
    /// integer.
    /// </summary>
    private static KdlNumber FromDouble(double value)
    {
        if (double.IsPositiveInfinity(value)) return KdlNumber.PositiveInfinity();
        if (double.IsNegativeInfinity(value)) return KdlNumber.NegativeInfinity();
        if (double.IsNaN(value)) return KdlNumber.NaN();

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var converted) || (converted == 0 && value != 0))
        {
            throw new NotSupportedException($"{text} is outside the range that can be written yet.");
        }

        if (converted.Scale == 0) converted *= 1.0m;

        return new(converted, KdlNumberFormat.Decimal);
    }

    /// <summary>
    /// Names and keys aren't KdlStrings, so there's no way to make KdlSharp quote one that it would
    /// otherwise write bare and so have read back as a number.
    /// </summary>
    private static void CheckName(string name, string description)
    {
        if (CouldReadAsNumber(name)) throw new NotSupportedException($"The {description} \"{name}\" can't be written yet.");
    }

    private static bool CouldReadAsNumber(string text)
    {
        var unsigned = text is ['+' or '-', .. var rest] ? rest : text;
        return unsigned is [>= '0' and <= '9', ..] or ['.', >= '0' and <= '9', ..];
    }
}
