using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Xml;

using KdlSharp;
using KdlSharp.Values;

namespace Sab39.KdlPs;

/// <summary>
/// Turns KdlSharp values into the .NET values Get-KdlNode outputs, applying type annotations. Anything
/// that can't be converted as annotated is left as it would be unannotated, and reported through warn.
/// </summary>
internal sealed class KdlValueConverter(Action<string> warn)
{
    private readonly Action<string> warn = warn;

    public object? ConvertValue(KdlValue value)
    {
        var annotation = value.TypeAnnotation?.TypeName;
        return value switch
        {
            KdlNumber number => this.ConvertNumber(number, annotation),
            KdlString text => this.ConvertString(text, annotation),
            _ => this.ConvertOther(value, annotation),
        };
    }

    /// <summary>
    /// A node's annotation is as likely to say what kind of node it is as to give its name a type, so
    /// the name is converted when a string annotation fits it and left alone otherwise, never warning.
    /// </summary>
    public static object ConvertNodeName(string name, string? annotation)
    {
        var converter = annotation is null ? null : GetStringConverter(annotation);
        return converter is null ? name : Apply(converter, name, out _) ?? name;
    }

    private object ConvertNumber(KdlNumber number, string? annotation)
    {
        if (number.IsSpecial) return this.ConvertSpecialNumber(number, annotation);

        var literal = KdlNumberParser.Parse(number.RawText ?? throw new InvalidOperationException("KdlSharp gave a parsed number no raw text."));
        if (annotation is null) return literal.ToDefaultValue();

        var converter = GetNumberConverter(annotation);
        if (converter is null)
        {
            this.Warn(number, GetStringConverter(annotation) is null
                ? $"Unrecognized type annotation ({annotation}); the number was left unconverted."
                : $"The ({annotation}) annotation applies to strings, not numbers; it was ignored.");
            return literal.ToDefaultValue();
        }

        var converted = Apply(converter, literal, out var failure);
        if (converted is not null) return converted;

        this.Warn(number, $"Couldn't convert the number to ({annotation}): {failure}");
        return literal.ToDefaultValue();
    }

    private object ConvertSpecialNumber(KdlNumber number, string? annotation)
    {
        var value = number.IsPositiveInfinity ? double.PositiveInfinity
            : number.IsNegativeInfinity ? double.NegativeInfinity
            : double.NaN;

        switch (annotation)
        {
            case null or "f64": return value;
            case "f32": return (float)value;

            default:
                this.Warn(number, $"The ({annotation}) annotation can't apply to {number.ToKdlString()}; it was ignored.");
                return value;
        }
    }

    private object ConvertString(KdlString text, string? annotation)
    {
        if (annotation is null) return text.Value;

        var converter = GetStringConverter(annotation);
        if (converter is null)
        {
            this.Warn(text, GetNumberConverter(annotation) is null
                ? $"Unrecognized type annotation ({annotation}); the value was left as a string."
                : $"The ({annotation}) annotation applies to numbers, not strings; the value was left as a string.");
            return text.Value;
        }

        var converted = Apply(converter, text.Value, out var failure);
        if (converted is not null) return converted;

        this.Warn(text, $"Couldn't convert \"{text.Value}\" to ({annotation}): {failure} The value was left as a string.");
        return text.Value;
    }

    private bool? ConvertOther(KdlValue value, string? annotation)
    {
        var result = value.IsNull() ? null : value.AsBoolean();
        if (annotation is not null) this.Warn(value, $"The ({annotation}) annotation can't apply to {value.ToKdlString()}; it was ignored.");
        return result;
    }

    private void Warn(KdlValue value, string message)
        => this.warn(value.SourcePosition is { } position ? $"Line {position.Line}, column {position.Column}: {message}" : message);

    /// <summary>Null when the conversion failed, with the reason in failure.</summary>
    private static object? Apply<T>(Func<T, object> converter, T input, out string? failure)
    {
        try
        {
            failure = null;
            return converter(input);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or OverflowException)
        {
            failure = e.Message;
            return null;
        }
    }

    /// <summary>Null when the annotation isn't one of the reserved numeric types.</summary>
    private static Func<KdlNumberLiteral, object>? GetNumberConverter(string annotation)
        => annotation switch
        {
            "i8" => n => n.ToInteger<sbyte>(),
            "i16" => n => n.ToInteger<short>(),
            "i32" => n => n.ToInteger<int>(),
            "i64" => n => n.ToInteger<long>(),
            "i128" => n => n.ToInteger<Int128>(),
            "u8" => n => n.ToInteger<byte>(),
            "u16" => n => n.ToInteger<ushort>(),
            "u32" => n => n.ToInteger<uint>(),
            "u64" => n => n.ToInteger<ulong>(),
            "u128" => n => n.ToInteger<UInt128>(),
            "isize" => n => n.ToInteger<nint>(),
            "usize" => n => n.ToInteger<nuint>(),
            "f32" => n => n.ToFloatingPoint<float>(),
            "f64" => n => n.ToFloatingPoint<double>(),
            "decimal64" or "decimal128" => n => n.ToFloatingPoint<decimal>(),
            _ => null,
        };

    /// <summary>
    /// Null when the annotation isn't one of the reserved string types. The reserved types with no
    /// better .NET type than string map to themselves, so they don't count as unrecognized.
    /// </summary>
    private static Func<string, object>? GetStringConverter(string annotation)
        => annotation switch
        {
            "date-time" => s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture),
            "date" => s => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            "time" => s => TimeOnly.Parse(s, CultureInfo.InvariantCulture),
            "duration" => s => XmlConvert.ToTimeSpan(s),
            "decimal" => s => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture),
            "email" or "idn-email" => s => new MailAddress(s),
            "country-2" => s => s.Length == 2 ? new RegionInfo(s) : throw new FormatException("Expected a two-letter country code."),
            "ipv4" => s => ParseAddress(s, AddressFamily.InterNetwork),
            "ipv6" => s => ParseAddress(s, AddressFamily.InterNetworkV6),
            "url" or "irl" => s => new Uri(s, UriKind.Absolute),
            "url-reference" or "irl-reference" => s => new Uri(s, UriKind.RelativeOrAbsolute),
            "uuid" => s => Guid.Parse(s, CultureInfo.InvariantCulture),
            "regex" => s => new Regex(s),
            "base64" => Convert.FromBase64String,
            "base85" => Ascii85.Decode,
            "currency" or "country-3" or "country-subdivision" or "hostname" or "idn-hostname" or "url-template" => s => s,
            _ => null,
        };

    private static IPAddress ParseAddress(string text, AddressFamily family)
        => IPAddress.Parse(text) is { } address && address.AddressFamily == family
            ? address
            : throw new FormatException($"Expected an {(family == AddressFamily.InterNetwork ? "IPv4" : "IPv6")} address.");
}
