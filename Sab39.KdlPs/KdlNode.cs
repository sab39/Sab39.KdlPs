namespace Sab39.KdlPs;

/// <summary>
/// The output of Get-KdlNode. The fixed members are prefixed with Node so that properties likely to
/// appear in real documents, like name=, can also get a generated member of their own.
/// </summary>
public sealed class KdlNode
{
    internal KdlNode(
        string nodeName,
        IReadOnlyList<object?> nodeArguments,
        OrderedDictionary<string, object?> nodeProperties,
        IReadOnlyList<KdlNode> childNodes,
        IReadOnlyList<KdlElement> rawNodeElements)
    {
        NodeName = nodeName;
        NodeArguments = nodeArguments;
        NodeProperties = nodeProperties;
        ChildNodes = childNodes;
        RawNodeElements = rawNodeElements;
        ConvenienceMembers = GeneratedMembers.Plan(nodeArguments, nodeProperties);
    }

    public string NodeName { get; }

    public IReadOnlyList<object?> NodeArguments { get; }

    /// <summary>Case-sensitive, in source order, with the last of any duplicate keys winning.</summary>
    public OrderedDictionary<string, object?> NodeProperties { get; }

    public IReadOnlyList<KdlNode> ChildNodes { get; }

    /// <summary>
    /// The name, then the arguments and properties interleaved as written, duplicates included. The
    /// interleaving is best effort: KdlSharp gives #true, #false and #null no source position, so one
    /// of those can land on the wrong side of an element of the other kind. Arguments stay in order
    /// among themselves, and so do properties.
    /// </summary>
    public IReadOnlyList<KdlElement> RawNodeElements { get; }

    /// <summary>
    /// The generated members the cmdlet attaches to this node's PSObject. Worked out here rather than
    /// there so the naming rules can be tested without PowerShell.
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, object?>> ConvenienceMembers { get; }

    public override string ToString() => NodeName;
}
