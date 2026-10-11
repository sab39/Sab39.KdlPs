using System.Management.Automation;

using KdlSharp;

namespace Sab39.KdlPs;

/// <summary>
/// Writes nodes as KDL text, serialized by KdlSharp. Everything piped in becomes one document, output
/// as a single string once the pipeline ends.
/// </summary>
[Cmdlet(VerbsCommunications.Write, "KdlNode")]
[OutputType(typeof(string))]
public sealed class WriteKdlNodeCommand : PSCmdlet
{
    private readonly KdlDocument document = new();

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public KdlNode[] InputObject { get; set; } = [];

    /// <summary>Writes the normalized form the official test suite's expected files use. So far, that only sorts properties.</summary>
    [Parameter]
    public SwitchParameter Normalize { get; set; }

    protected override void ProcessRecord()
    {
        foreach (var node in InputObject)
        {
            try
            {
                this.document.Nodes.Add(KdlSharpConverter.ToKdlSharp(node, Normalize));
            }
            catch (NotSupportedException e)
            {
                ThrowTerminatingError(new(e, "KdlWriteNotSupported", ErrorCategory.NotImplemented, node));
            }
        }
    }

    /// <summary>With no nodes, that's an empty string: an empty document is still a document.</summary>
    protected override void EndProcessing() => WriteObject(this.document.ToKdlString().TrimEnd('\n'));
}
