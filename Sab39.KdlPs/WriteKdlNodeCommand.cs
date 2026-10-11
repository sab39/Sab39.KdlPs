using System.Management.Automation;

using KdlSharp;

using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// Writes nodes as KDL text, serialized by KdlSharp from the nodes backing them. Everything piped in
/// becomes one document, output as a single string once the pipeline ends.
/// </summary>
[Cmdlet(VerbsCommunications.Write, "KdlNode")]
[OutputType(typeof(string))]
public sealed class WriteKdlNodeCommand : PSCmdlet
{
    private readonly KdlDocument document = new();

    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public KdlNode[] InputObject { get; set; } = [];

    /// <summary>
    /// Writes the normalized form the official test suite's expected files use: KdlSharp's canonical
    /// form, with properties sorted by key and only the last of any duplicate kept.
    /// </summary>
    [Parameter]
    public SwitchParameter Normalize { get; set; }

    protected override void ProcessRecord()
    {
        foreach (var node in InputObject)
        {
            try
            {
                this.document.Nodes.Add(Normalize ? Normalized(node.Backing) : node.Backing);
            }
            catch (NotSupportedException e)
            {
                ThrowTerminatingError(new(e, "KdlWriteNotSupported", ErrorCategory.NotImplemented, node));
            }
        }
    }

    /// <summary>With no nodes, that's an empty string: an empty document is still a document.</summary>
    protected override void EndProcessing() => WriteObject(this.document.ToKdlString().TrimEnd('\n'));

    /// <summary>A normalized copy, leaving the node itself as it is.</summary>
    private static KdlSharpNode Normalized(KdlSharpNode node)
    {
        var copy = node.Clone();
        NormalizeProperties(copy);
        return copy;
    }

    private static void NormalizeProperties(KdlSharpNode node)
    {
        KdlProperty[] properties = [.. node.Properties.GroupBy(p => p.Key, StringComparer.Ordinal)
            .Select(g => g.Last())
            .OrderBy(p => p.Key, StringComparer.Ordinal)];
        node.Properties.Clear();
        foreach (var property in properties) node.Properties.Add(property);

        foreach (var child in node.Children) NormalizeProperties(child);
    }
}
