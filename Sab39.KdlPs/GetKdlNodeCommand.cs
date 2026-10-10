using System.Management.Automation;

namespace Sab39.KdlPs;

[Cmdlet(VerbsCommon.Get, "KdlNode", DefaultParameterSetName = PathSet)]
public sealed class GetKdlNodeCommand : PSCmdlet
{
    private const string PathSet = "Path";
    private const string LiteralPathSet = "LiteralPath";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [SupportsWildcards]
    public string[] Path { get; set; } = [];

    // PSPath is what lets Get-ChildItem output pipe straight in: every provider item carries one.
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("PSPath", "LP")]
    public string[] LiteralPath { get; set; } = [];

    protected override void ProcessRecord() => throw new NotImplementedException();
}
