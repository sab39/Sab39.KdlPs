using System.Management.Automation;

using KdlSharp;
using KdlSharp.Settings;

namespace Sab39.KdlPs;

/// <summary>
/// Writes nodes as KDL text, serialized by KdlSharp. Everything piped in becomes one document, output
/// as a single string once the pipeline ends.
/// </summary>
[Cmdlet(VerbsCommunications.Write, "KdlNode")]
[OutputType(typeof(string))]
public sealed class WriteKdlNodeCommand : PSCmdlet
{
    // Bare strings are only written when string types are preserved; PreferIdentifierStrings alone
    // has no effect.
    private static readonly KdlFormatterSettings settings = new() { PreserveStringTypes = true };

    private readonly KdlDocument document = new();

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public KdlNode[] InputObject { get; set; } = [];

    protected override void ProcessRecord()
    {
        foreach (var node in InputObject)
        {
            try
            {
                this.document.Nodes.Add(KdlSharpConverter.ToKdlSharp(node));
            }
            catch (NotSupportedException e)
            {
                ThrowTerminatingError(new(e, "KdlWriteNotSupported", ErrorCategory.NotImplemented, node));
            }
        }
    }

    protected override void EndProcessing()
    {
        if (this.document.Nodes.Count == 0) return;

        WriteObject(this.document.ToKdlString(settings).TrimEnd('\n'));
    }
}
