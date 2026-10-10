using System.Globalization;
using System.Numerics;

namespace Sab39.KdlPs;

/// <summary>
/// A KDL number as written, with underscores removed. Digits is everything after the sign and any
/// radix prefix - including the fraction and exponent, for a decimal literal that has them.
/// </summary>
internal readonly record struct KdlNumberLiteral(bool IsNegative, int Radix, string Digits, bool IsIntegral)
{
    private string text => IsNegative ? $"-{Digits}" : Digits;

    public BigInteger ToBigInteger()
    {
        if (!IsIntegral) throw new FormatException($"{text} isn't an integer.");
        if (Radix == 10) return BigInteger.Parse(text, CultureInfo.InvariantCulture);

        var magnitude = BigInteger.Zero;
        foreach (var digit in Digits) magnitude = (magnitude * Radix) + HexDigitValue(digit);

        return IsNegative ? -magnitude : magnitude;
    }

    /// <summary>Throws FormatException for a non-integer, and OverflowException when out of range.</summary>
    public T ToInteger<T>()
        where T : IBinaryInteger<T>
        => T.CreateChecked(ToBigInteger());

    public T ToFloatingPoint<T>()
        where T : IFloatingPoint<T>
        => Radix == 10
            ? T.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
            : T.CreateChecked(ToBigInteger());

    /// <summary>
    /// The value for a number with no annotation, typed by how it's written. Integer literals become
    /// the first of int, long and BigInteger to fit. Anything with a fraction or exponent becomes a
    /// decimal when that can hold it, and a double otherwise.
    /// </summary>
    public object ToDefaultValue()
    {
        if (IsIntegral)
        {
            var value = ToBigInteger();
            if (value >= int.MinValue && value <= int.MaxValue) return (int)value;
            if (value >= long.MinValue && value <= long.MaxValue) return (long)value;
            return value;
        }

        // Zero from a mantissa with a non-zero digit in it means the decimal underflowed.
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            && (exact != 0 || !HasNonZeroMantissa()))
        {
            return exact;
        }

        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private bool HasNonZeroMantissa() => Digits.TakeWhile(c => c is not ('e' or 'E')).Any(c => c is >= '1' and <= '9');

    private static int HexDigitValue(char digit) => char.IsAsciiDigit(digit) ? digit - '0' : char.ToLowerInvariant(digit) - 'a' + 10;
}
