using KdlSharpNode = KdlSharp.KdlNode;

namespace Sab39.KdlPs;

/// <summary>
/// The output of Get-KdlNode. The fixed members are prefixed with Node so that properties likely to
/// appear in real documents, like name=, can also get a generated member of their own.
/// </summary>
public sealed class KdlNode
{
    private readonly Func<KdlSharpNode>? createBacking;

    /// <summary>A node read from KDL, backed by the KdlSharp node it was read from.</summary>
    internal KdlNode(
        KdlSharpNode backing,
        IReadOnlyList<object?> nodeArguments,
        OrderedDictionary<string, object?> nodeProperties,
        IReadOnlyList<KdlNode> childNodes,
        IReadOnlyList<KdlElement> rawNodeElements)
        : this(backing.Name, nodeArguments, nodeProperties, childNodes, rawNodeElements)
    {
        Backing = backing;
    }

    /// <summary>
    /// A node built from .NET values, backed by a KdlSharp node created on first use. Creating it throws
    /// NotSupportedException for values that can't be written.
    /// </summary>
    internal KdlNode(
        string nodeName,
        IReadOnlyList<object?> nodeArguments,
        OrderedDictionary<string, object?> nodeProperties,
        IReadOnlyList<KdlNode> childNodes,
        IReadOnlyList<KdlElement> rawNodeElements,
        Func<KdlSharpNode> createBacking)
        : this(nodeName, nodeArguments, nodeProperties, childNodes, rawNodeElements)
    {
        this.createBacking = createBacking;
    }

    private KdlNode(
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

    /// <summary>The name, then the arguments and properties interleaved as written, duplicates included.</summary>
    public IReadOnlyList<KdlElement> RawNodeElements { get; }

    /// <summary>
    /// The KdlSharp node this one is written from. For now, changes made to this node after it's created
    /// aren't reflected in it.
    /// </summary>
    internal KdlSharpNode Backing
    {
        get => field ??= this.createBacking!();
        private init;
    }

    /// <summary>
    /// The generated members the cmdlet attaches to this node's PSObject. Worked out here rather than
    /// there so the naming rules can be tested without PowerShell.
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, object?>> ConvenienceMembers { get; }

    public override string ToString() => NodeName;
}
