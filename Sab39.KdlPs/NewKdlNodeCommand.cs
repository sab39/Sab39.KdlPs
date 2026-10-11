using System.Collections;
using System.Management.Automation;

namespace Sab39.KdlPs;

/// <summary>
/// Creates a node of the same kind Get-KdlNode outputs. Arguments can be given positionally after the
/// name, so New-KdlNode foo bar baz is New-KdlNode -NodeName foo -NodeArguments bar, baz.
/// </summary>
[Cmdlet(VerbsCommon.New, "KdlNode")]
[OutputType(typeof(KdlNode))]
public sealed class NewKdlNodeCommand : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string NodeName { get; set; } = "";

    [Parameter(ValueFromRemainingArguments = true)]
    public object?[] NodeArguments { get; set; } = [];

    /// <summary>A hashtable, an ordered dictionary, or a custom object.</summary>
    [Parameter]
    public object? NodeProperties { get; set; }

    [Parameter]
    public KdlNode[] ChildNodes { get; set; } = [];

    protected override void ProcessRecord()
    {
        OrderedDictionary<string, object?> properties = new(StringComparer.Ordinal);
        if (!AddProperties(properties, NodeProperties))
        {
            ThrowTerminatingError(new(
                new ArgumentException("NodeProperties must be a hashtable, an ordered dictionary or a custom object."),
                "InvalidNodeProperties",
                ErrorCategory.InvalidArgument,
                NodeProperties));
        }

        object?[] arguments = [.. NodeArguments.Select(Unwrap)];
        KdlElement[] elements =
        [
            new(KdlElementKind.NodeName, null, NodeName, null, null),
            .. arguments.Select(a => new KdlElement(KdlElementKind.Argument, null, a, null, null)),
            .. properties.Select(p => new KdlElement(KdlElementKind.Property, p.Key, p.Value, null, null)),
        ];

        // The backing node is built from the values as given, like a read node's from what was read
        KdlNode[] childNodes = [.. ChildNodes];
        KeyValuePair<string, object?>[] givenProperties = [.. properties];
        var nodeName = NodeName;
        KdlNode node = new(
            nodeName,
            arguments,
            properties,
            childNodes,
            elements,
            () => KdlSharpConverter.ToKdlSharp(nodeName, arguments, givenProperties, childNodes));
        WriteObject(GetKdlNodeCommand.AttachMembers(node));
    }

    /// <summary>False when the input isn't any of the accepted kinds.</summary>
    private static bool AddProperties(OrderedDictionary<string, object?> properties, object? input)
    {
        switch (input)
        {
            case null: return true;

            case PSObject { BaseObject: PSCustomObject } custom:
                foreach (var property in custom.Properties) properties[property.Name] = Unwrap(property.Value);
                return true;

            case PSObject wrapped: return AddProperties(properties, wrapped.BaseObject);

            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary) properties[$"{entry.Key}"] = Unwrap(entry.Value);
                return true;

            default: return false;
        }
    }

    private static object? Unwrap(object? value) => value is PSObject { BaseObject: not PSCustomObject } wrapped ? wrapped.BaseObject : value;
}
