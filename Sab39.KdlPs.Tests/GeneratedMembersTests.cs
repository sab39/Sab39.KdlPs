namespace Sab39.KdlPs.Tests;

[TestClass]
public sealed class GeneratedMembersTests
{
    [TestMethod]
    [DataRow("first-name", "FirstName")]
    [DataRow("first_name", "FirstName")]
    [DataRow("firstName", "FirstName")]
    [DataRow("FirstName", "FirstName")]
    [DataRow("FIRST_NAME", "FIRSTNAME")]
    [DataRow("a.b c", "ABC")]
    [DataRow("x1-y", "X1Y")]
    [DataRow("née", "Née")]
    [DataRow("--", "")]
    public void ConvertsToPascalCase(string key, string expected)
        => Assert.AreEqual(expected, GeneratedMembers.ToPascalCase(key));

    [TestMethod]
    public void FirstKeyWinsAmongConvertedNames()
        => AssertPlan([("foo-bar", 1), ("foo_bar", 2)], [("FooBar", 1)]);

    [TestMethod]
    public void ExactNameWinsOverConvertedNamesWhereverItIs()
        => AssertPlan([("foo-bar", 1), ("fooBar", 2), ("FooBar", 3)], [("FooBar", 3)]);

    [TestMethod]
    public void ClashesAreCaseInsensitive()
        => AssertPlan([("name", 1), ("Name", 2)], [("Name", 2)]);

    [TestMethod]
    public void FirstExactNameWinsWhenSeveralDifferOnlyInCase()
        => AssertPlan([("NAME", 1), ("Name", 2)], [("NAME", 1)]);

    [TestMethod]
    public void KeysWithNoLettersOrDigitsGetNoMember()
        => AssertPlan([("--", 1), ("a", 2)], [("A", 2)]);

    [TestMethod]
    public void MembersFollowSourceOrder()
        => AssertPlan([("b", 1), ("a", 2), ("c", 3)], [("B", 1), ("A", 2), ("C", 3)]);

    [TestMethod]
    public void SingleArgumentGetsArgumentAndArg1()
        => AssertPlan(["x"], [], [("Argument", "x"), ("Arg1", "x")]);

    [TestMethod]
    public void SeveralArgumentsGetNumberedMembersOnly()
        => AssertPlan([1, 2, 3], [], [("Arg1", 1), ("Arg2", 2), ("Arg3", 3)]);

    [TestMethod]
    public void NoArgumentsGetNoArgumentMembers()
        => AssertPlan([], [("a", 1)], [("A", 1)]);

    [TestMethod]
    public void ArgumentMembersComeBeforePropertyMembers()
        => AssertPlan(["x"], [("a", 1)], [("Argument", "x"), ("Arg1", "x"), ("A", 1)]);

    [TestMethod]
    public void PropertiesWinOverArgumentMembers()
        => AssertPlan(["x", "y"], [("arg1", 1), ("ARGUMENT", 2)], [("Arg2", "y"), ("Arg1", 1), ("ARGUMENT", 2)]);

    [TestMethod]
    public void PropertiesWinOverTheSingleArgumentMember()
        => AssertPlan(["x"], [("argument", 1)], [("Arg1", "x"), ("Argument", 1)]);

    private static void AssertPlan(object?[] arguments, (string Key, object? Value)[] properties, (string Name, object? Value)[] expected)
    {
        var plan = GeneratedMembers.Plan(arguments, properties.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)));

        CollectionAssert.AreEqual(expected, plan.Select(m => (m.Key, m.Value)).ToArray());
    }

    private static void AssertPlan((string Key, object? Value)[] properties, (string Name, object? Value)[] expected)
    {
        var plan = GeneratedMembers.Plan([], properties.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)));

        CollectionAssert.AreEqual(expected, plan.Select(m => (m.Key, m.Value)).ToArray());
    }
}
