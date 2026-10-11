using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Sab39.KdlPs.Tests;

/// <summary>
/// An in-process PowerShell with the module's cmdlets and format file registered directly, rather
/// than through the module manifest.
/// </summary>
internal static class ModuleShell
{
    // The format file ships next to the module's own assembly, wherever the test runner has put it.
    private static readonly string formatFile = Path.Combine(
        Path.GetDirectoryName(typeof(GetKdlNodeCommand).Assembly.Location)!,
        "Sab39.KdlPs.format.ps1xml");

    public sealed record Result(PSObject[] Output, string[] Warnings, ErrorRecord[] Errors);

    /// <summary>Runs a script taking one parameter, and collects its output, warnings and errors.</summary>
    public static Result Run(string script, object? argument = null)
    {
        var state = InitialSessionState.CreateDefault();

        // Execution policy applies to format files too, and a hosted PowerShell with no config of its
        // own defaults to Restricted, which silently skips loading them. Setting ExecutionPolicy here
        // comes too late to help, so the policy-enforcing authorization manager is swapped for the
        // base one, which allows everything. (A shell started with -ExecutionPolicy Bypass hides all
        // this, by passing its policy on to the test process through the environment.)
        state.AuthorizationManager = new AuthorizationManager("Sab39.KdlPs.Tests");
        state.Commands.Add(new SessionStateCmdletEntry("Get-KdlNode", typeof(GetKdlNodeCommand), null));
        state.Commands.Add(new SessionStateCmdletEntry("New-KdlNode", typeof(NewKdlNodeCommand), null));
        state.Commands.Add(new SessionStateCmdletEntry("Write-KdlNode", typeof(WriteKdlNodeCommand), null));
        state.Formats.Add(new SessionStateFormatEntry(formatFile));

        using var shell = PowerShell.Create(state);
        shell.AddScript(script).AddArgument(argument);
        var output = shell.Invoke();

        return new([.. output], [.. shell.Streams.Warning.Select(w => w.Message)], [.. shell.Streams.Error]);
    }

    public static object? Member(PSObject node, string name) => node.Properties[name]?.Value;

    public static string[] NodeNames(IEnumerable<PSObject> nodes) => [.. nodes.Select(n => (string)Member(n, "NodeName")!)];
}
