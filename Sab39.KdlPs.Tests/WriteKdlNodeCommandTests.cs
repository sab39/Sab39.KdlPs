using System.Globalization;

using static Sab39.KdlPs.Tests.ModuleShell;

namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class WriteKdlNodeCommandTests
{
    private const string NumbersAreDecimal = "KdlSharp holds every number as a decimal";
    private const string ExponentFormatting = "KdlSharp writes numbers from 1e10 up, or below 1e-4, in E notation with only 7 significant digits";
    private const string ZeroFractionDropped = "KdlSharp drops a zero fraction";
    private const string BareNumberLikeNames = "KdlSharp writes number-like node names and property keys bare";
    private const string ValueAnnotationsNotWritten = "value annotations aren't written yet";

    [TestMethod]
    public void NewNodeArgumentsAreWritten() => Assert.AreEqual("foo bar baz", Written("New-KdlNode foo bar baz"));

    [TestMethod]
    public void PipedNodesBecomeOneDocument() => Assert.AreEqual("a\nb", Written("Get-KdlNode -Text 'a; b'"));

    [TestMethod]
    public void NoInputGivesNoOutput() => Assert.IsEmpty(Run("@() | Write-KdlNode").Output);

    [TestMethod]
    public void PropertiesFollowArgumentsInDictionaryOrder()
        => Assert.AreEqual("n 1 b=2 a=x", Written("New-KdlNode n 1 -NodeProperties ([ordered]@{ b = 2; a = 'x' })"));

    [TestMethod]
    public void ChildrenAreIndentedInABlock()
        => Assert.AreEqual("p {\n    c 1\n    d {\n        e\n    }\n}", Written("Get-KdlNode -Text 'p { c 1; d { e; }; }'"));

    [TestMethod]
    public void KeywordsAndSpecialNumbersRoundTrip()
        => Assert.AreEqual("n #true #false #null #inf #-inf #nan", Written("Get-KdlNode -Text 'n #true #false #null #inf #-inf #nan'"));

    [TestMethod]
    public void NodeNameAnnotationIsWritten() => Assert.AreEqual("(t)a 1", Written("Get-KdlNode -Text '(t)a 1'"));

    [TestMethod]
    [DataRow("x")]
    [DataRow("x-1")]
    [DataRow("-")]
    [DataRow(".x")]
    public void IdentifierStringsAreBare(string value) => Assert.AreEqual($"n {value}", Written("New-KdlNode n $arg", value));

    [TestMethod]
    [DataRow("has space", "\"has space\"")]
    [DataRow("", "\"\"")]
    [DataRow("true", "\"true\"")]
    [DataRow("a\"b", "\"a\\\"b\"")]
    [DataRow("1x", "\"1x\"")]
    [DataRow("-1", "\"-1\"")]
    [DataRow("+1", "\"+1\"")]
    [DataRow(".5", "\".5\"")]
    [DataRow("-.5", "\"-.5\"")]
    public void OtherStringsAreQuoted(string value, string expected) => Assert.AreEqual($"n {expected}", Written("New-KdlNode n $arg", value));

    [TestMethod]
    public void IntegersAreWrittenExactly() => Assert.AreEqual("n 1 -2 9999999999", Written("New-KdlNode n 1 (-2) 9999999999"));

    // Unparenthesized, -0.25 would reach New-KdlNode as a string: as a remaining argument, PowerShell
    // takes it for a parameter name.
    [TestMethod]
    public void FractionsAreWrittenExactly() => Assert.AreEqual("n 1.5 -0.25 0.0001", Written("New-KdlNode n 1.5 (-0.25) 0.0001"));

    [TestMethod]
    public void LargeIntegersAreWrittenExactly()
        => ExpectedFailure.Run(ExponentFormatting, () => Assert.AreEqual(
            "n 10000000000 9223372036854775807",
            Written("New-KdlNode n 10000000000 ([long]::MaxValue)")));

    [TestMethod]
    public void NumberLikeNodeNamesAreQuoted()
        => ExpectedFailure.Run(BareNumberLikeNames, () => Assert.AreEqual("\"1x\"", Written("New-KdlNode 1x")));

    [TestMethod]
    public void NumberLikePropertyKeysAreQuoted()
        => ExpectedFailure.Run(BareNumberLikeNames, () => Assert.AreEqual("n \"-1\"=1", Written("New-KdlNode n -NodeProperties @{ '-1' = 1 }")));

    [TestMethod]
    public void IntegersBeyondDecimalAreWrittenExactly()
        => ExpectedFailure.Run(NumbersAreDecimal, () => Assert.AreEqual(
            "n 340282366920938463463374607431768211456",
            Written("New-KdlNode n ([bigint]::Pow(2, 128))")));

    [TestMethod]
    [DataRow("1e300")]
    [DataRow("-1e300")]
    [DataRow("1e-30")]
    public void DoublesBeyondDecimalAreWritten(string value)
        => ExpectedFailure.Run(NumbersAreDecimal, () => Assert.AreEqual(
            double.Parse(value, CultureInfo.InvariantCulture),
            double.Parse(WrittenArgument($"New-KdlNode n ([double]'{value}')"), NumberStyles.Float, CultureInfo.InvariantCulture)));

    [TestMethod]
    [DataRow("79228162514264337593543950335")]
    [DataRow("123456789012345678901234567")]
    [DataRow("0.000000000000000000001234567890123")]
    public void DecimalsKeepTheirPrecision(string value)
        => ExpectedFailure.Run(ExponentFormatting, () => Assert.AreEqual(
            decimal.Parse(value, CultureInfo.InvariantCulture),
            decimal.Parse(WrittenArgument($"New-KdlNode n ([decimal]'{value}')"), NumberStyles.Float, CultureInfo.InvariantCulture)));

    // Written without a fraction or exponent, it would read back as an integer.
    [TestMethod]
    public void WholeDecimalsStayFractional()
        => ExpectedFailure.Run(ZeroFractionDropped, () => Assert.AreNotEqual(
            -1,
            WrittenArgument("New-KdlNode n ([decimal]'1.0')").IndexOfAny(['.', 'e', 'E'])));

    [TestMethod]
    [DataRow("(u8)5")]
    [DataRow("(date)\"2020-01-01\"")]
    [DataRow("(uuid)\"0f8fad5b-d9cb-469f-a165-70867728950e\"")]
    public void AnnotatedValuesRoundTrip(string value)
        => ExpectedFailure.Run(ValueAnnotationsNotWritten, () => Assert.AreEqual($"n {value}", Written("Get-KdlNode -Text $arg", $"n {value}")));

    /// <summary>
    /// Pipes the nodes a script outputs to Write-KdlNode. A terminating error comes back as the output
    /// instead, so that a test expecting text fails on an assertion that shows the error.
    /// </summary>
    private static string Written(string nodes, object? argument = null)
        => (string)Run($$"""param($arg) try { {{nodes}} | Write-KdlNode } catch { "Error: $_" }""", argument).Output.Single().BaseObject;

    /// <summary>The text of the one argument of the one node a script outputs.</summary>
    private static string WrittenArgument(string nodes)
    {
        var written = Written(nodes);
        return written.StartsWith("n ", StringComparison.Ordinal) ? written[2..] : throw new AssertFailedException($"Unexpected output: {written}");
    }
}
