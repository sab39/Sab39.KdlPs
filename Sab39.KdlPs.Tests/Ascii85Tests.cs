using System.Text;

namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class Ascii85Tests
{
    [TestMethod]
    [DataRow("9jqo^", "Man ")]
    [DataRow("<~9jqo^~>", "Man ")]
    [DataRow("9j qo\n^", "Man ")]
    [DataRow("9jqo", "Man")]
    [DataRow("", "")]
    public void DecodesText(string encoded, string expected)
        => Assert.AreEqual(expected, Encoding.ASCII.GetString(Ascii85.Decode(encoded)));

    [TestMethod]
    public void DecodesZAsFourZeroBytes()
        => CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 77, 97, 110, 32 }, Ascii85.Decode("z9jqo^"));

    [TestMethod]
    [DataRow("9jqo^v")]
    [DataRow("9jz")]
    [DataRow("9jqo^9")]
    [DataRow("uuuuu")]
    public void RejectsInvalidInput(string encoded)
        => Assert.ThrowsExactly<FormatException>(() => Ascii85.Decode(encoded));
}
