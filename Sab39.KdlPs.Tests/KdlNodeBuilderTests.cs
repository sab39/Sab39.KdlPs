using System.Globalization;
using System.Net;
using System.Net.Mail;

using KdlSharp;

namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class KdlNodeBuilderTests
{
    [TestMethod]
    public void SplitsArgumentsAndProperties()
    {
        var (node, warnings) = Build("n 1 \"s\" a=1 #true #null b=bare");

        Assert.AreEqual("n", node.NodeName);
        CollectionAssert.AreEqual(new object?[] { 1, "s", true, null }, node.NodeArguments.ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b" }, node.NodeProperties.Keys.ToArray());
        Assert.AreEqual("bare", node.NodeProperties["b"]);
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    public void DuplicatePropertiesKeepFirstPositionAndLastValue()
    {
        var (node, _) = Build("n a=1 b=2 a=3");

        CollectionAssert.AreEqual(new[] { "a", "b" }, node.NodeProperties.Keys.ToArray());
        Assert.AreEqual(3, node.NodeProperties["a"]);
        CollectionAssert.AreEqual(new KeyValuePair<string, object?>[] { new("A", 3), new("B", 2) }, node.ConvenienceMembers.ToArray());
    }

    [TestMethod]
    public void PropertyKeysAreCaseSensitive()
    {
        var (node, _) = Build("n Name=1 name=2");

        Assert.AreEqual(1, node.NodeProperties["Name"]);
        Assert.AreEqual(2, node.NodeProperties["name"]);
    }

    [TestMethod]
    public void RawElementsInterleaveInSourceOrder()
    {
        var (node, _) = Build("n 1 a=2 3 a=4");

        CollectionAssert.AreEqual(
            new[] { KdlElementKind.NodeName, KdlElementKind.Argument, KdlElementKind.Property, KdlElementKind.Argument, KdlElementKind.Property },
            node.RawNodeElements.Select(e => e.Kind).ToArray());
        CollectionAssert.AreEqual(new object?[] { "n", 1, 2, 3, 4 }, node.RawNodeElements.Select(e => e.Value).ToArray());
        CollectionAssert.AreEqual(new[] { null, null, "a", null, "a" }, node.RawNodeElements.Select(e => e.Key).ToArray());
    }

    [TestMethod]
    public void KeywordValuesKeepTheirOrderWithinTheirOwnKind()
    {
        var (node, _) = Build("n #false a=#true 1 #null b=2 c=#false");

        CollectionAssert.AreEqual(new object?[] { false, 1, null }, node.NodeArguments.ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, node.NodeProperties.Keys.ToArray());
        CollectionAssert.AreEqual(new object?[] { true, 2, false }, node.NodeProperties.Values.ToArray());
    }

    [TestMethod]
    public void KeywordArgumentsInterleaveWithPropertiesInSourceOrder()
        => ExpectedFailure.Run(KeywordPositionsMissing, () =>
        {
            var (node, _) = Build("n 1 a=2 #true");

            CollectionAssert.AreEqual(
                new[] { KdlElementKind.NodeName, KdlElementKind.Argument, KdlElementKind.Property, KdlElementKind.Argument },
                node.RawNodeElements.Select(e => e.Kind).ToArray());
        });

    [TestMethod]
    public void KeywordPropertiesInterleaveWithArgumentsInSourceOrder()
        => ExpectedFailure.Run(KeywordPositionsMissing, () =>
        {
            var (node, _) = Build("n 1 a=#null");

            CollectionAssert.AreEqual(
                new[] { KdlElementKind.NodeName, KdlElementKind.Argument, KdlElementKind.Property },
                node.RawNodeElements.Select(e => e.Kind).ToArray());
        });

    [TestMethod]
    public void KeywordValuesHaveSourcePositions()
        => ExpectedFailure.Run(KeywordPositionsMissing, () =>
        {
            var (node, _) = Build("n #true a=#false #null");

            CollectionAssert.AreEqual(new int?[] { 0, 2, 10, 17 }, node.RawNodeElements.Select(e => e.SourcePosition?.Offset).ToArray());
        });

    private const string KeywordPositionsMissing = "KdlSharp gives #true, #false and #null no source position";

    [TestMethod]
    public void RawElementsCarryAnnotationsAndPositions()
    {
        var (node, _) = Build("(kind)n (u8)1 a=(f32)2");

        CollectionAssert.AreEqual(new[] { "kind", "u8", "f32" }, node.RawNodeElements.Select(e => e.Annotation).ToArray());
        CollectionAssert.AreEqual(new int?[] { 6, 12, 21 }, node.RawNodeElements.Select(e => e.SourcePosition?.Offset).ToArray());
    }

    [TestMethod]
    public void BuildsChildren()
    {
        var (node, _) = Build("parent {\n  child 1\n  child 2 {\n    grandchild\n  }\n}");

        CollectionAssert.AreEqual(new[] { "child", "child" }, node.ChildNodes.Select(c => c.NodeName).ToArray());
        Assert.AreEqual(2, node.ChildNodes[1].NodeArguments[0]);
        Assert.AreEqual("grandchild", node.ChildNodes[1].ChildNodes[0].NodeName);
    }

    [TestMethod]
    public void NumbersAreReadFromTheSource()
    {
        var (node, _) = Build("n 1e-30 9223372036854775808 1.50");

        Assert.AreEqual(1e-30, node.NodeArguments[0]);
        Assert.AreEqual(System.Numerics.BigInteger.Parse("9223372036854775808", CultureInfo.InvariantCulture), node.NodeArguments[1]);
        Assert.AreEqual("1.50", Convert.ToString(node.NodeArguments[2], CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void SpecialNumbersBecomeDoublesOrAnnotatedFloats()
    {
        var (node, warnings) = Build("n #inf #-inf (f32)#inf (u8)#nan");

        Assert.AreEqual(double.PositiveInfinity, node.NodeArguments[0]);
        Assert.AreEqual(double.NegativeInfinity, node.NodeArguments[1]);
        Assert.AreEqual(float.PositiveInfinity, node.NodeArguments[2]);
        Assert.IsTrue(node.NodeArguments[3] is double d && double.IsNaN(d));
        Assert.HasCount(1, warnings);
    }

    [TestMethod]
    public void AnnotatedNumbersConvert()
    {
        var (node, warnings) = Build("n (u8)255 (i128)-1 (f32)1.5 (f64)2 (decimal64)1e3 (usize)7");

        CollectionAssert.AreEqual(new object?[] { (byte)255, (Int128)(-1), 1.5f, 2.0, 1000m, (nuint)7 }, node.NodeArguments.ToArray());
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    [DataRow("(u8)300", 300)]
    [DataRow("(i32)1.5", 1.5)]
    [DataRow("(foo)5", 5)]
    [DataRow("(uuid)5", 5)]
    public void UnconvertibleNumbersFallBackToUnannotatedWithAWarning(string argument, object expected)
    {
        var (node, warnings) = Build($"n {argument}");

        Assert.AreEqual(Convert.ToDecimal(expected, CultureInfo.InvariantCulture), Convert.ToDecimal(node.NodeArguments[0], CultureInfo.InvariantCulture));
        Assert.HasCount(1, warnings);
        StringAssert.StartsWith(warnings[0], "Line 1, column ");
    }

    [TestMethod]
    public void AnnotatedStringsConvert()
    {
        var (node, warnings) = Build("""
            n (uuid)"6ba7b810-9dad-11d1-80b4-00c04fd430c8" (date)"2024-01-02" (email)"a@b.c" (ipv4)"1.2.3.4" (url)"https://kdl.dev/" (duration)"PT1H30M" (country-2)"NZ" (decimal)"1.25"
            """);

        Assert.AreEqual(Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8", CultureInfo.InvariantCulture), node.NodeArguments[0]);
        Assert.AreEqual(new DateOnly(2024, 1, 2), node.NodeArguments[1]);
        Assert.AreEqual(new MailAddress("a@b.c"), node.NodeArguments[2]);
        Assert.AreEqual(IPAddress.Parse("1.2.3.4"), node.NodeArguments[3]);
        Assert.AreEqual(new Uri("https://kdl.dev/"), node.NodeArguments[4]);
        Assert.AreEqual(TimeSpan.FromMinutes(90), node.NodeArguments[5]);
        Assert.AreEqual(new RegionInfo("NZ"), node.NodeArguments[6]);
        Assert.AreEqual(1.25m, node.NodeArguments[7]);
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    public void BinaryAnnotationsDecode()
    {
        var (node, _) = Build("n (base64)\"AQID\" (base85)\"9jqo^\"");

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, (byte[])node.NodeArguments[0]!);
        CollectionAssert.AreEqual("Man "u8.ToArray(), (byte[])node.NodeArguments[1]!);
    }

    [TestMethod]
    public void ReservedStringTypesWithNoDotNetTypeStayStringsSilently()
    {
        var (node, warnings) = Build("n (hostname)\"kdl.dev\" (currency)\"NZD\"");

        CollectionAssert.AreEqual(new object?[] { "kdl.dev", "NZD" }, node.NodeArguments.ToArray());
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    [DataRow("(ipv4)\"::1\"")]
    [DataRow("(uuid)\"nope\"")]
    [DataRow("(foo)\"x\"")]
    [DataRow("(u8)\"12\"")]
    public void UnconvertibleStringsStayStringsWithAWarning(string argument)
    {
        var (node, warnings) = Build($"n {argument}");

        Assert.IsInstanceOfType<string>(node.NodeArguments[0]);
        Assert.HasCount(1, warnings);
    }

    [TestMethod]
    public void AnnotatedBooleansAndNullsWarn()
    {
        var (node, warnings) = Build("n (u8)#true (foo)#null");

        CollectionAssert.AreEqual(new object?[] { true, null }, node.NodeArguments.ToArray());
        Assert.HasCount(2, warnings);
    }

    [TestMethod]
    public void NodeNameAnnotationsConvertTheRawElementOnly()
    {
        var (node, warnings) = Build("(date)\"2024-01-02\" 1");

        Assert.AreEqual("2024-01-02", node.NodeName);
        Assert.AreEqual(new DateOnly(2024, 1, 2), node.RawNodeElements[0].Value);
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    [DataRow("(contributor)person")]
    [DataRow("(date)birthday")]
    public void NodeNameAnnotationsNeverWarn(string source)
    {
        var (node, warnings) = Build(source);

        Assert.IsInstanceOfType<string>(node.RawNodeElements[0].Value);
        Assert.IsEmpty(warnings);
    }

    private static (KdlNode Node, List<string> Warnings) Build(string source)
    {
        List<string> warnings = [];
        KdlNodeBuilder builder = new(source, warnings.Add);
        return (builder.Build(KdlDocument.Parse(source).Nodes[0]), warnings);
    }
}
