using System.Text;

namespace Sab39.KdlPs;

/// <summary>
/// Reads a number from KDL source text. KdlSharp only keeps numbers as decimals, which loses range
/// and precision, but it does say where each one starts - so they're re-read from there.
/// </summary>
internal static class KdlNumberParser
{
    /// <summary>
    /// Reads the number starting at offset, and checks that what follows it could legitimately end a
    /// number. KdlSharp has already validated the document, so a FormatException from here means this
    /// reader and KdlSharp disagree about where the number is.
    /// </summary>
    public static KdlNumberLiteral Read(string source, int offset)
    {
        var position = offset;
        var isNegative = false;
        if (position < source.Length && source[position] is '+' or '-')
        {
            isNegative = source[position] == '-';
            position++;
        }

        var radix = position + 1 < source.Length && source[position] == '0'
            ? source[position + 1] switch
            {
                'x' => 16,
                'o' => 8,
                'b' => 2,
                _ => 10,
            }
            : 10;

        StringBuilder digits = new();
        var isIntegral = true;
        if (radix != 10)
        {
            position = ReadDigits(source, position + 2, radix, digits);
        }
        else
        {
            position = ReadDigits(source, position, radix, digits);
            if (position + 1 < source.Length && source[position] == '.' && IsDigit(source[position + 1], radix))
            {
                digits.Append('.');
                position = ReadDigits(source, position + 1, radix, digits);
                isIntegral = false;
            }

            if (position < source.Length && source[position] is 'e' or 'E')
            {
                digits.Append('e');
                position++;
                if (position < source.Length && source[position] is '+' or '-')
                {
                    digits.Append(source[position]);
                    position++;
                }

                position = ReadDigits(source, position, radix, digits);
                isIntegral = false;
            }
        }

        if (position < source.Length && !IsDelimiter(source[position]))
        {
            throw new FormatException($"Unexpected '{source[position]}' after the number at offset {offset}.");
        }

        return new(isNegative, radix, $"{digits}", isIntegral);
    }

    /// <summary>
    /// Reads a run of digits and underscores, which has to start with a digit, appending only the
    /// digits. Returns the position after the run.
    /// </summary>
    private static int ReadDigits(string source, int position, int radix, StringBuilder digits)
    {
        if (position >= source.Length || !IsDigit(source[position], radix))
        {
            throw new FormatException($"Expected a base-{radix} digit at offset {position}.");
        }

        for (; position < source.Length && (source[position] == '_' || IsDigit(source[position], radix)); position++)
        {
            if (source[position] != '_') digits.Append(source[position]);
        }

        return position;
    }

    private static bool IsDigit(char c, int radix)
        => radix switch
        {
            16 => char.IsAsciiHexDigit(c),
            8 => c is >= '0' and <= '7',
            2 => c is '0' or '1',
            _ => char.IsAsciiDigit(c),
        };

    private static bool IsDelimiter(char c) => char.IsWhiteSpace(c) || c is ';' or '{' or '}' or '/' or '\\' or '﻿';
}
