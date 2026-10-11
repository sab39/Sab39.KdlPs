using System.Management.Automation;
using System.Net.Mail;

using static Sab39.KdlPs.Tests.ModuleShell;

namespace Sab39.KdlPs.Tests;

/// <summary>Runs Get-KdlNode in an in-process PowerShell.</summary>
[TestClass]
public sealed class GetKdlNodeCommandTests
{
    [TestMethod]
    public void TextInputProducesTopLevelNodes()
    {
        var result = Run("param($kdl) Get-KdlNode -Text $kdl", "a 1; b 2");

        CollectionAssert.AreEqual(new[] { "a", "b" }, NodeNames(result.Output));
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void EmptyTextIsAnEmptyDocument()
    {
        var result = Run("param($kdl) Get-KdlNode -Text $kdl", "");

        Assert.IsEmpty(result.Output);
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public void GeneratedMembersSupportSelectObject()
    {
        var result = Run(
            "param($kdl) Get-KdlNode -Text $kdl -Query author | Select-Object Name, Argument, Email, Role",
            """
            authors {
                author "Ada Lovelace" email=(email)"ada@example.com" role=lead
                author "Charles Babbage" email=(email)"cb@example.com"
            }
            """);

        Assert.HasCount(2, result.Output);
        Assert.AreEqual("author", Member(result.Output[0], "Name"));
        Assert.AreEqual("Ada Lovelace", Member(result.Output[0], "Argument"));
        Assert.AreEqual(new MailAddress("ada@example.com"), Member(result.Output[0], "Email"));
        Assert.AreEqual("lead", Member(result.Output[0], "Role"));
        Assert.IsNull(Member(result.Output[1], "Role"));
    }

    [TestMethod]
    public void ArgumentMembersAreNumbered()
    {
        var node = Run("param($kdl) Get-KdlNode -Text $kdl", "n 1 two #true").Output.Single();

        Assert.AreEqual(1, Member(node, "Arg1"));
        Assert.AreEqual("two", Member(node, "Arg2"));
        Assert.AreEqual(true, Member(node, "Arg3"));
        Assert.IsNull(node.Properties["Argument"]);
    }

    [TestMethod]
    public void NameAliasesNodeNameWhenNoPropertyIsCalledName()
        => Assert.AreEqual("n", Member(Run("param($kdl) Get-KdlNode -Text $kdl", "n a=1").Output.Single(), "Name"));

    [TestMethod]
    [DataRow("n name=prop")]
    [DataRow("n NAME=prop")]
    public void NamePropertyWinsOverTheAlias(string kdl)
        => Assert.AreEqual("prop", Member(Run("param($kdl) Get-KdlNode -Text $kdl", kdl).Output.Single(), "Name"));

    [TestMethod]
    public void PropertiesClashingWithExistingMembersStayInNodeProperties()
    {
        var node = Run("param($kdl) Get-KdlNode -Text $kdl", "n nodename=a GetType=b").Output.Single();

        Assert.AreEqual("n", Member(node, "NodeName"));
        Assert.IsInstanceOfType<PSMethodInfo>(node.Members["GetType"]);
        var properties = (OrderedDictionary<string, object?>)Member(node, "NodeProperties")!;
        Assert.AreEqual("a", properties["nodename"]);
        Assert.AreEqual("b", properties["GetType"]);
    }

    [TestMethod]
    public void ChildNodesKeepTheirGeneratedMembers()
    {
        var result = Run("param($kdl) $node = Get-KdlNode -Text $kdl; $node.ChildNodes[0].X; $node.ChildNodes[0].Argument", "p { c 5 x=1; }");

        CollectionAssert.AreEqual(new object[] { 1, 5 }, result.Output.Select(o => o.BaseObject).ToArray());
    }

    [TestMethod]
    public void RecurseOutputsDescendantsDepthFirst()
        => CollectionAssert.AreEqual(
            new[] { "a", "b", "c", "e", "d" },
            NodeNames(Run("param($kdl) Get-KdlNode -Text $kdl -Recurse", "a { b { c; }; e; }; d").Output));

    [TestMethod]
    public void RecursedNodesHaveGeneratedMembers()
    {
        var result = Run("param($kdl) Get-KdlNode -Text $kdl -Recurse | Select-Object -Last 1", "a { b x=1; }");

        Assert.AreEqual(1, Member(result.Output.Single(), "X"));
    }

    [TestMethod]
    public void QuerySelectsMatchingNodes()
        => CollectionAssert.AreEqual(
            new[] { "b", "b" },
            NodeNames(Run("param($kdl) Get-KdlNode -Text $kdl -Query 'a >> b'", "a { b 1; c { b 2; }; }; b 3").Output));

    [TestMethod]
    public void QueryAndRecurseCombine()
        => CollectionAssert.AreEqual(
            new[] { "c", "b" },
            NodeNames(Run("param($kdl) Get-KdlNode -Text $kdl -Query 'a > c' -Recurse", "a { b; c { b; }; }").Output));

    // Only a terminating error is caught by try/catch, so getting the error back from the catch is
    // what shows it was one.
    [TestMethod]
    public void InvalidQueryIsATerminatingError()
        => Assert.AreEqual(
            "InvalidKdlQuery,Sab39.KdlPs.GetKdlNodeCommand",
            Run("param($kdl) try { Get-KdlNode -Text $kdl -Query 'a b' } catch { $_.FullyQualifiedErrorId }", "a").Output.Single().BaseObject);

    [TestMethod]
    public void ParseErrorsAreNotCaughtAsTerminating()
        => Assert.AreEqual(
            "not caught",
            Run("param($kdl) try { Get-KdlNode -Text $kdl -ErrorAction Continue 2>$null; 'not caught' } catch { 'caught' }", "a \"unterminated\n").Output.Single().BaseObject);

    [TestMethod]
    public void ParseErrorsAreNonTerminating()
    {
        var result = Run("param($kdl) Get-KdlNode -Text $kdl; 'after'", "a \"unterminated\n");

        Assert.AreEqual("KdlParseFailed,Sab39.KdlPs.GetKdlNodeCommand", result.Errors.Single().FullyQualifiedErrorId);
        Assert.AreEqual("after", result.Output.Single().BaseObject);
    }

    [TestMethod]
    public void TooLargeNumbersAreParseErrors()
        => Assert.AreEqual(
            "KdlParseFailed,Sab39.KdlPs.GetKdlNodeCommand",
            Run("param($kdl) Get-KdlNode -Text $kdl", "n 1e200").Errors.Single().FullyQualifiedErrorId);

    [TestMethod]
    public void ConversionWarningsFromTextHaveNoFilePrefix()
        => Assert.AreEqual(
            "Line 1, column 7: Couldn't convert the number to (u8):",
            Run("param($kdl) Get-KdlNode -Text $kdl", "n (u8)300").Warnings.Single()[..54]);

    [TestMethod]
    public void PathAcceptsWildcards()
    {
        using TempFolder folder = new();
        folder.Write("one.kdl", "one");
        folder.Write("two.kdl", "two");
        folder.Write("other.txt", "other");

        CollectionAssert.AreEquivalent(
            new[] { "one", "two" },
            NodeNames(Run("param($folder) Get-KdlNode (Join-Path $folder '*.kdl')", folder.Path).Output));
    }

    [TestMethod]
    public void LiteralPathDoesNotExpandWildcards()
    {
        using TempFolder folder = new();
        folder.Write("[x].kdl", "bracketed");

        var result = Run("param($folder) Get-KdlNode -LiteralPath (Join-Path $folder '[x].kdl')", folder.Path);

        CollectionAssert.AreEqual(new[] { "bracketed" }, NodeNames(result.Output));
    }

    [TestMethod]
    public void FilesPipeInFromGetChildItem()
    {
        using TempFolder folder = new();
        folder.Write("one.kdl", "one");
        folder.Write("two.kdl", "two");

        CollectionAssert.AreEquivalent(
            new[] { "one", "two" },
            NodeNames(Run("param($folder) Get-ChildItem $folder -Filter *.kdl | Get-KdlNode", folder.Path).Output));
    }

    [TestMethod]
    public void MissingFilesAreNonTerminatingErrors()
    {
        using TempFolder folder = new();
        folder.Write("present.kdl", "present");

        var result = Run("param($folder) Get-KdlNode (Join-Path $folder 'missing.kdl'), (Join-Path $folder 'present.kdl')", folder.Path);

        Assert.AreEqual("PathNotFound,Sab39.KdlPs.GetKdlNodeCommand", result.Errors.Single().FullyQualifiedErrorId);
        CollectionAssert.AreEqual(new[] { "present" }, NodeNames(result.Output));
    }

    [TestMethod]
    public void ConversionWarningsFromFilesArePrefixedWithThePath()
    {
        using TempFolder folder = new();
        var file = folder.Write("warn.kdl", "n (u8)300");

        StringAssert.StartsWith(Run("param($file) Get-KdlNode -LiteralPath $file", file).Warnings.Single(), $"{file}: Line 1, column 7:");
    }

    [TestMethod]
    public void DefaultViewIsATableOfNameArgumentsPropertiesAndChildren()
    {
        // Whether headers get ANSI emphasis depends on the environment the tests run in, so it's
        // turned off to compare plain text.
        var result = Run(
            "param($kdl) $PSStyle.OutputRendering = 'PlainText'; Get-KdlNode -Text $kdl | Out-String -Width 200",
            "n 1 \"s\" #null a=2 b=#null { c; d; }");
        var text = string.Concat(result.Output);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        CollectionAssert.AreEqual(
            new[] { "NodeName", "Arguments", "Properties", "ChildNodes" },
            lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries),
            $"Formatted output was:\n{text}");
        StringAssert.Contains(lines[2], "1 s #null");
        StringAssert.Contains(lines[2], "a=2 b=#null");
        StringAssert.Contains(lines[2], "c, d");
    }

    /// <summary>A folder of test files, deleted on dispose.</summary>
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Sab39.KdlPs.Tests-{Guid.NewGuid():N}")).FullName;

        public string Write(string name, string content)
        {
            var file = System.IO.Path.Combine(Path, name);
            File.WriteAllText(file, content);
            return file;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
