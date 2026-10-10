using System.Globalization;
using System.Numerics;

namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class KdlNumberParserTests
{
    [TestMethod]
    [DataRow("1", typeof(int), "1")]
    [DataRow("-5", typeof(int), "-5")]
    [DataRow("+7", typeof(int), "7")]
    [DataRow("1_000", typeof(int), "1000")]
    [DataRow("2147483648", typeof(long), "2147483648")]
    [DataRow("-9223372036854775808", typeof(long), "-9223372036854775808")]
    [DataRow("9223372036854775808", typeof(BigInteger), "9223372036854775808")]
    [DataRow("0xFF", typeof(int), "255")]
    [DataRow("-0x10", typeof(int), "-16")]
    [DataRow("0xdead_beef", typeof(long), "3735928559")]
    [DataRow("0o17", typeof(int), "15")]
    [DataRow("0b101", typeof(int), "5")]
    [DataRow("1.50", typeof(decimal), "1.50")]
    [DataRow("0.0", typeof(decimal), "0.0")]
    [DataRow("1e3", typeof(decimal), "1000")]
    [DataRow("1.5E+2", typeof(decimal), "150")]
    [DataRow("0.1234567890123456789012345678901", typeof(decimal), "0.1234567890123456789012345679")]
    [DataRow("1e-30", typeof(double), "1E-30")]
    [DataRow("1e200", typeof(double), "1E+200")]
    public void DefaultValueIsTypedByHowTheNumberIsWritten(string text, Type expectedType, string expectedValue)
    {
        var value = KdlNumberParser.Read(text, 0).ToDefaultValue();

        Assert.AreEqual(expectedType, value.GetType());
        Assert.AreEqual(expectedValue, Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [DataRow("n 0xFF a=1", 2, "255")]
    [DataRow("n 1;", 2, "1")]
    [DataRow("n {\n  c 42}", 8, "42")]
    [DataRow("n 3 // comment", 2, "3")]
    [DataRow("n 4\r\n", 2, "4")]
    public void ReadsFromAnOffsetUpToADelimiter(string source, int offset, string expectedValue)
        => Assert.AreEqual(expectedValue, Convert.ToString(KdlNumberParser.Read(source, offset).ToDefaultValue(), CultureInfo.InvariantCulture));

    [TestMethod]
    [DataRow("1abc")]
    [DataRow("1\"")]
    [DataRow("0x")]
    [DataRow("_1")]
    [DataRow("1e")]
    public void RejectsWhatIsNotACompleteNumber(string text)
        => Assert.ThrowsExactly<FormatException>(() => KdlNumberParser.Read(text, 0));

    [TestMethod]
    public void IntegerConversionChecksRange()
    {
        Assert.AreEqual((byte)255, KdlNumberParser.Read("255", 0).ToInteger<byte>());
        Assert.ThrowsExactly<OverflowException>(() => KdlNumberParser.Read("256", 0).ToInteger<byte>());
        Assert.AreEqual(UInt128.MaxValue, KdlNumberParser.Read("340282366920938463463374607431768211455", 0).ToInteger<UInt128>());
    }

    [TestMethod]
    public void IntegerConversionRejectsFractions()
        => Assert.ThrowsExactly<FormatException>(() => KdlNumberParser.Read("1.5", 0).ToInteger<int>());

    [TestMethod]
    public void FloatingPointConversionAcceptsAnyLiteral()
    {
        Assert.AreEqual(1.5f, KdlNumberParser.Read("1.5", 0).ToFloatingPoint<float>());
        Assert.AreEqual(255.0, KdlNumberParser.Read("0xFF", 0).ToFloatingPoint<double>());
        Assert.AreEqual(1000m, KdlNumberParser.Read("1e3", 0).ToFloatingPoint<decimal>());
    }
}
