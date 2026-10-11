using System.Management.Automation;

using static Sab39.KdlPs.Tests.ModuleShell;

namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class NewKdlNodeCommandTests
{
    [TestMethod]
    [DataRow("New-KdlNode foo bar baz")]
    [DataRow("New-KdlNode -NodeName foo -NodeArguments bar, baz")]
    public void ArgumentsCanBePositional(string script)
    {
        var node = Run(script).Output.Single();

        Assert.AreEqual("foo", Member(node, "NodeName"));
        CollectionAssert.AreEqual(new object[] { "bar", "baz" }, Arguments(node));
    }

    [TestMethod]
    public void ArgumentsKeepTheirTypes()
        => CollectionAssert.AreEqual(
            new object?[] { 1, true, null, "x", 1.5 },
            Arguments(Run("New-KdlNode n 1 $true $null x 1.5").Output.Single()));

    [TestMethod]
    public void ArgumentsAreUnwrapped()
        => Assert.IsInstanceOfType<int>(Arguments(Run("New-KdlNode n (Write-Output 1)").Output.Single())[0]);

    [TestMethod]
    public void OrderedDictionaryPropertiesKeepTheirOrder()
        => CollectionAssert.AreEqual(
            new[] { "b=2", "a=x" },
            Properties(Run("New-KdlNode n -NodeProperties ([ordered]@{ b = 2; a = 'x' })").Output.Single()));

    [TestMethod]
    public void HashtablePropertiesAreAccepted()
        => CollectionAssert.AreEquivalent(
            new[] { "a=1", "b=2" },
            Properties(Run("New-KdlNode n -NodeProperties @{ a = 1; b = 2 }").Output.Single()));

    [TestMethod]
    public void CustomObjectPropertiesAreAccepted()
        => CollectionAssert.AreEqual(
            new[] { "b=2", "a=x" },
            Properties(Run("New-KdlNode n -NodeProperties ([pscustomobject]@{ b = 2; a = 'x' })").Output.Single()));

    [TestMethod]
    public void OtherPropertyInputIsATerminatingError()
        => Assert.AreEqual(
            "InvalidNodeProperties,Sab39.KdlPs.NewKdlNodeCommand",
            Run("try { New-KdlNode n -NodeProperties 5 } catch { $_.FullyQualifiedErrorId }").Output.Single().BaseObject);

    [TestMethod]
    public void ChildNodesAcceptBothCmdletsOutput()
    {
        var node = Run("New-KdlNode p -ChildNodes (New-KdlNode a), (Get-KdlNode -Text b)").Output.Single();

        CollectionAssert.AreEqual(
            new[] { "a", "b" },
            ((IReadOnlyList<KdlNode>)Member(node, "ChildNodes")!).Select(c => c.NodeName).ToArray());
    }

    [TestMethod]
    public void GeneratedMembersAreAttached()
    {
        var node = Run("New-KdlNode n 5 -NodeProperties @{ 'first-name' = 'Ada' }").Output.Single();

        Assert.AreEqual(5, Member(node, "Argument"));
        Assert.AreEqual("Ada", Member(node, "FirstName"));
        Assert.AreEqual("n", Member(node, "Name"));
    }

    [TestMethod]
    public void RawNodeElementsListNameArgumentsThenProperties()
    {
        var node = (KdlNode)Run("New-KdlNode n 1 2 -NodeProperties @{ a = 3 }").Output.Single().BaseObject;

        CollectionAssert.AreEqual(
            new[] { "NodeName:n", "Argument:1", "Argument:2", "Property:a=3" },
            node.RawNodeElements.Select(e => $"{e.Kind}:{e}").ToArray());
    }

    private static object?[] Arguments(PSObject node) => [.. (IReadOnlyList<object?>)Member(node, "NodeArguments")!];

    private static string[] Properties(PSObject node)
        => [.. ((OrderedDictionary<string, object?>)Member(node, "NodeProperties")!).Select(p => $"{p.Key}={p.Value}")];
}
