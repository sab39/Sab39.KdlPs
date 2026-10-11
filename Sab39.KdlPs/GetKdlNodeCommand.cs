using System.Management.Automation;

using KdlSharp;
using KdlSharp.Exceptions;
using KdlSharp.Query;
using Microsoft.PowerShell.Commands;

namespace Sab39.KdlPs;

[Cmdlet(VerbsCommon.Get, "KdlNode", DefaultParameterSetName = PathSet)]
[OutputType(typeof(KdlNode))]
public sealed class GetKdlNodeCommand : PSCmdlet
{
    private const string PathSet = "Path";
    private const string LiteralPathSet = "LiteralPath";
    private const string TextSet = "Text";

    private CompiledQuery? compiledQuery;

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathSet, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [SupportsWildcards]
    public string[] Path { get; set; } = [];

    // PSPath is what lets Get-ChildItem output pipe straight in: every provider item carries one.
    [Parameter(Mandatory = true, ParameterSetName = LiteralPathSet, ValueFromPipelineByPropertyName = true)]
    [Alias("PSPath", "LP")]
    public string[] LiteralPath { get; set; } = [];

    /// <summary>KDL source to read directly, instead of from a file. Empty text is an empty document.</summary>
    [Parameter(Mandatory = true, ParameterSetName = TextSet)]
    [AllowEmptyString]
    public string Text { get; set; } = "";

    /// <summary>A KDL query; only the nodes it matches are output.</summary>
    [Parameter]
    public string? Query { get; set; }

    /// <summary>Outputs every descendant of each node too, depth-first.</summary>
    [Parameter]
    public SwitchParameter Recurse { get; set; }

    protected override void BeginProcessing()
    {
        if (Query is null) return;

        try
        {
            this.compiledQuery = KdlQuery.Compile(Query);
        }
        catch (KdlQueryException e)
        {
            ThrowTerminatingError(new(e, "InvalidKdlQuery", ErrorCategory.InvalidArgument, Query));
        }
    }

    protected override void ProcessRecord()
    {
        if (ParameterSetName == TextSet)
        {
            ProcessText(Text, null);
            return;
        }

        var isLiteral = ParameterSetName == LiteralPathSet;
        foreach (var path in isLiteral ? LiteralPath : Path)
        {
            foreach (var file in ResolveFiles(path, isLiteral)) ReadFile(file);
        }
    }

    private IEnumerable<string> ResolveFiles(string path, bool isLiteral)
    {
        ProviderInfo provider;
        IEnumerable<string> files;
        try
        {
            files = isLiteral
                ? [SessionState.Path.GetUnresolvedProviderPathFromPSPath(path, out provider, out _)]
                : SessionState.Path.GetResolvedProviderPathFromPSPath(path, out provider);
        }
        catch (ItemNotFoundException e)
        {
            WriteError(new(e, "PathNotFound", ErrorCategory.ObjectNotFound, path));
            return [];
        }

        if (provider.ImplementingType != typeof(FileSystemProvider))
        {
            WriteError(new(
                new ArgumentException($"{path} isn't a file system path."),
                "NotFileSystemPath",
                ErrorCategory.InvalidArgument,
                path));
            return [];
        }

        return files;
    }

    private void ReadFile(string file)
    {
        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            WriteError(new(e, "FileReadFailed", ErrorCategory.ReadError, file));
            return;
        }

        ProcessText(text, file);
    }

    /// <summary>Parses and outputs one document. File is null when the text came from -Text.</summary>
    private void ProcessText(string text, string? file)
    {
        KdlDocument document;
        try
        {
            document = KdlDocument.Parse(text);
        }
        catch (KdlException e)
        {
            WriteError(new(e, "KdlParseFailed", ErrorCategory.ParserError, file ?? text));
            return;
        }

        KdlNodeBuilder builder = new(text, message => WriteWarning(file is null ? message : $"{file}: {message}"));
        foreach (var node in this.compiledQuery?.Execute(document) ?? document.Nodes)
        {
            var built = builder.Build(node);
            WriteObject(AttachMembers(built));
            if (Recurse) WriteObject(Descendants(built).Select(PSObject.AsPSObject), enumerateCollection: true);
        }
    }

    private static IEnumerable<KdlNode> Descendants(KdlNode node)
        => node.ChildNodes.SelectMany(child => (IEnumerable<KdlNode>)[child, .. Descendants(child)]);

    /// <summary>
    /// Adds the generated property members to a node and its descendants. PowerShell keeps members
    /// added this way with the node itself, so they're still there when it's reached via ChildNodes.
    /// </summary>
    internal static PSObject AttachMembers(KdlNode node)
    {
        var wrapper = PSObject.AsPSObject(node);
        foreach (var (name, value) in node.ConvenienceMembers)
        {
            // A name PowerShell already has a member for - one of KdlNode's own, or one every object
            // has, like GetType - is left alone. The value is still in NodeProperties.
            if (wrapper.Members[name] is null) wrapper.Members.Add(new PSNoteProperty(name, value));
        }

        if (wrapper.Members["Name"] is null && !node.NodeProperties.Keys.Any(k => k.Equals("name", StringComparison.OrdinalIgnoreCase)))
        {
            wrapper.Members.Add(new PSAliasProperty("Name", nameof(KdlNode.NodeName)));
        }

        foreach (var child in node.ChildNodes) AttachMembers(child);

        return wrapper;
    }
}
