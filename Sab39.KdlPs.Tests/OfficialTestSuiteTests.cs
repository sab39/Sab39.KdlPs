using System.Text.RegularExpressions;

using static Sab39.KdlPs.Tests.ModuleShell;

namespace Sab39.KdlPs.Tests;

/// <summary>
/// Runs the official KDL test suite, from the specs submodule, through the cmdlets. Every input is a
/// test case of each method.
/// </summary>
[TestClass]
public sealed class OfficialTestSuiteTests
{
    private const string MissingSubmodule = "(specs submodule missing)";

    private static readonly string? testCases = FindTestCases();

    public static IEnumerable<object[]> Inputs
        => testCases is null
            ? [[MissingSubmodule]]
            : from file in Directory.GetFiles(Path.Combine(testCases, "input"), "*.kdl")
              let name = Path.GetFileNameWithoutExtension(file)
              orderby name
              select new object[] { name };

    public static IEnumerable<object[]> InputsWithExpectedOutput => Inputs.Where(i => !IsFailCase((string)i[0]));

    /// <summary>Each input with expected output, and its expected file too unless that's identical to it.</summary>
    public static IEnumerable<object[]> RoundTripCases
        => from i in InputsWithExpectedOutput
           let name = (string)i[0]
           from side in name == MissingSubmodule || ExpectedIsIdentical(name) ? [InputSide] : new[] { InputSide, ExpectedSide }
           select new object[] { name, side };

    private const string InputSide = "input";
    private const string ExpectedSide = "expected";

    /// <summary>
    /// The input reads as the same nodes as its expected file does, or for a _fail input, gives a
    /// parse error.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(Inputs))]
    public void ReadMatchesRead(string name)
        => RunCase(name, () =>
        {
            var actual = Run("param($path) Get-KdlNode -LiteralPath $path", InputFile(name));
            if (IsFailCase(name))
            {
                Assert.IsEmpty(actual.Output, "A _fail input gave nodes.");
                Assert.AreEqual("KdlParseFailed,Sab39.KdlPs.GetKdlNodeCommand", actual.Errors.SingleOrDefault()?.FullyQualifiedErrorId, $"Errors: {string.Join("; ", actual.Errors)}");
                return;
            }

            CompareNodes(Read(InputFile(name), "input"), Read(ExpectedFile(name), "expected file"), "");
        });

    /// <summary>Writing what the input reads as, normalized, reproduces its expected file exactly.</summary>
    [TestMethod]
    [DynamicData(nameof(InputsWithExpectedOutput))]
    public void WriteMatchesExpected(string name)
        => RunCase(name, () =>
        {
            var result = Run("""param($path) try { Get-KdlNode -LiteralPath $path | Write-KdlNode -Normalize } catch { "Error: $_" }""", InputFile(name));
            Assert.IsEmpty(result.Errors, $"Reading the input failed: {string.Join("; ", result.Errors)}");
            var written = (string)result.Output.Single().BaseObject;

            // Compared as lines, the way PowerShell shows the output string. That also takes either line
            // ending in the expected file, which Git's autocrlf can check out as CRLF.
            CollectionAssert.AreEqual(File.ReadAllLines(ExpectedFile(name)), written.Split('\n'), $"Written:\n{written}");
        });

    /// <summary>
    /// Writing what a file reads as, without normalizing, reads back as the same nodes. That shows the
    /// output parses to the same values, without prescribing what form it takes.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(RoundTripCases))]
    public void ReadWriteReadMatchesRead(string name, string side)
        => RunCase(name, () =>
        {
            var file = side == InputSide ? InputFile(name) : ExpectedFile(name);
            var original = Read(file, side);

            var result = Run("""param($path) try { Get-KdlNode -LiteralPath $path | Write-KdlNode } catch { "Error: $_" }""", file);
            var written = (string)result.Output.Single().BaseObject;

            var reread = Run("param($kdl) Get-KdlNode -Text $kdl", written);
            Assert.IsEmpty(reread.Errors, $"Reading back failed: {string.Join("; ", reread.Errors)}\nWritten:\n{written}");
            CompareNodes([.. reread.Output.Select(o => (KdlNode)o.BaseObject)], original, "");
        });

    private static void RunCase(string name, Action test)
    {
        if (name == MissingSubmodule) Assert.Fail("The specs submodule isn't initialized. Run: git submodule update --init");

        test();
    }

    private static KdlNode[] Read(string file, string description)
    {
        var result = Run("param($path) Get-KdlNode -LiteralPath $path", file);
        Assert.IsEmpty(result.Errors, $"Reading the {description} failed: {string.Join("; ", result.Errors)}");
        return [.. result.Output.Select(o => (KdlNode)o.BaseObject)];
    }

    private static void CompareNodes(IReadOnlyList<KdlNode> actual, IReadOnlyList<KdlNode> expected, string path)
    {
        Assert.HasCount(expected.Count, actual, $"Node count at /{path}");
        for (var i = 0; i < expected.Count; i++)
        {
            var (a, e) = (actual[i], expected[i]);
            var here = $"{path}/{e.NodeName}[{i}]";

            Assert.AreEqual(e.NodeName, a.NodeName, $"Name at {here}");
            Assert.AreEqual(e.RawNodeElements[0].Annotation, a.RawNodeElements[0].Annotation, $"Annotation at {here}");
            CompareElements(Arguments(a), Arguments(e), $"Arguments at {here}");
            CompareElements(Properties(a), Properties(e), $"Properties at {here}");
            CompareNodes(a.ChildNodes, e.ChildNodes, here);
        }
    }

    private static List<KdlElement> Arguments(KdlNode node) => [.. node.RawNodeElements.Where(e => e.Kind == KdlElementKind.Argument)];

    /// <summary>The rightmost of each key, sorted, as expected files have them.</summary>
    private static List<KdlElement> Properties(KdlNode node)
        => [.. node.RawNodeElements.Where(e => e.Kind == KdlElementKind.Property).GroupBy(e => e.Key!).Select(g => g.Last()).OrderBy(e => e.Key, StringComparer.Ordinal)];

    private static void CompareElements(List<KdlElement> actual, List<KdlElement> expected, string what)
    {
        string describe(IEnumerable<KdlElement> elements)
            => string.Join(" ", elements.Select(e => $"{e.Key}{(e.Key is null ? "" : "=")}({e.Annotation}){e.Value}<{e.Value?.GetType().Name}>"));

        var matches = actual.Count == expected.Count
            && actual.Zip(expected).All(p => p.First.Key == p.Second.Key && p.First.Annotation == p.Second.Annotation && AreSameValue(p.First.Value, p.Second.Value));
        Assert.IsTrue(matches, $"{what}: expected {describe(expected)}, got {describe(actual)}");
    }

    private static bool AreSameValue(object? actual, object? expected)
        => (actual, expected) switch
        {
            (null, null) => true,
            (null, _) or (_, null) => false,
            _ when actual.GetType() != expected.GetType() => false,
            (byte[] a, byte[] e) => a.SequenceEqual(e),
            (Regex a, Regex e) => $"{a}" == $"{e}" && a.Options == e.Options,
            _ => actual.Equals(expected),
        };

    private static bool IsFailCase(string name) => name.EndsWith("_fail", StringComparison.Ordinal);

    private static bool ExpectedIsIdentical(string name) => File.ReadAllText(InputFile(name)) == File.ReadAllText(ExpectedFile(name));

    private static string InputFile(string name) => Path.Combine(testCases!, "input", $"{name}.kdl");

    private static string ExpectedFile(string name) => Path.Combine(testCases!, "expected_kdl", $"{name}.kdl");

    /// <summary>The test suite in the specs submodule at the repo root, or null if it isn't there.</summary>
    private static string? FindTestCases()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (!File.Exists(Path.Combine(folder.FullName, "Sab39.KdlPs.slnx"))) continue;

            var testCases = Path.Combine(folder.FullName, "specs", "tests", "test_cases");
            return Directory.Exists(testCases) ? testCases : null;
        }

        return null;
    }
}
