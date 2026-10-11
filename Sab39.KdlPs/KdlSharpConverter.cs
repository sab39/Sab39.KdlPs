using System.Globalization;
using System.Management.Automation;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

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

    /// <summary>The annotation a value is written with, or null for none.</summary>
    public static string? GetAnnotation(object? value) => GetIntegerAnnotation(value) ?? ToAnnotatedText(value)?.Annotation;

    private static KdlValue ToKdlValue(object? value)
    {
        var result = ToKdlValueWithoutIntegerAnnotation(value);
        if (GetIntegerAnnotation(value) is { } annotation) result.TypeAnnotation = new KdlAnnotation(annotation);
        return result;
    }

    /// <summary>
    /// Integer types smaller than int, and unsigned ones, are annotated: a byte was probably meant as one.
    /// The larger signed types are read back by magnitude.
    /// </summary>
    private static string? GetIntegerAnnotation(object? value)
        => value switch
        {
            PSObject wrapped => GetIntegerAnnotation(wrapped.BaseObject),
            sbyte => "i8",
            short => "i16",
            byte => "u8",
            ushort => "u16",
            uint => "u32",
            ulong => "u64",
            nuint => "usize",
            UInt128 => "u128",
            _ => null,
        };

    private static KdlValue ToKdlValueWithoutIntegerAnnotation(object? value)
        => value switch
        {
            null => KdlNull.Instance,
            PSObject wrapped => ToKdlValueWithoutIntegerAnnotation(wrapped.BaseObject),
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

            _ when ToAnnotatedText(value) is var (annotation, text) => new KdlString(text) { TypeAnnotation = new KdlAnnotation(annotation) },
            _ => throw new NotSupportedException($"Values of type {value.GetType()} can't be written yet."),
        };

    /// <summary>
    /// Values of the types Get-KdlNode reads from annotated strings, as the annotation and the text it
    /// reads back from. Null for anything else.
    /// </summary>
    private static (string Annotation, string Text)? ToAnnotatedText(object? value)
        => value switch
        {
            PSObject wrapped => ToAnnotatedText(wrapped.BaseObject),
            Guid g => ("uuid", g.ToString("D")),
            DateTimeOffset d => ("date-time", FormatDateTime(d)),

            // Read back as a DateTimeOffset. Local and unspecified times get the local offset.
            DateTime d => ("date-time", FormatDateTime(new DateTimeOffset(d))),

            DateOnly d => ("date", d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            TimeOnly t => ("time", t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
            TimeSpan t => ("duration", XmlConvert.ToString(t)),
            MailAddress m => ("email", $"{m}"),
            RegionInfo r => ("country-2", r.TwoLetterISORegionName),
            IPAddress a => (a.AddressFamily == AddressFamily.InterNetworkV6 ? "ipv6" : "ipv4", $"{a}"),
            Uri u => (u.IsAbsoluteUri ? "url" : "url-reference", u.OriginalString),

            // Only the pattern: options have no place in the text
            Regex r => ("regex", $"{r}"),

            byte[] b => ("base64", Convert.ToBase64String(b)),
            _ => null,
        };

    /// <summary>ISO 8601, with a fraction of a second only when there is one.</summary>
    private static string FormatDateTime(DateTimeOffset value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
}
