namespace Sab39.KdlPs.Tests;

/// <summary>
/// MSTest has no expected-failure attribute, so this stands in for one: a test of the correct behaviour
/// that's known not to hold yet. While its assertions fail it reports inconclusive; once they pass, it
/// fails, so the fix gets noticed and the wrapper removed.
/// </summary>
internal static class ExpectedFailure
{
    public static void Run(string reason, Action test)
    {
        try
        {
            test();
        }
        catch (AssertFailedException e)
        {
            Assert.Inconclusive($"Expected failure - {reason}: {e.Message}");
        }

        Assert.Fail($"Passed, but is marked as an expected failure ({reason}). Remove the ExpectedFailure wrapper.");
    }
}
