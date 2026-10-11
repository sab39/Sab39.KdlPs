# Editing nodes

Making the nodes `Get-KdlNode` and `New-KdlNode` output editable in PowerShell, as naturally as possible, and writable with `Write-KdlNode` so that only what was edited changes. This builds on the `kdlsharp-parsing-fixes` branch, where each node is backed by a KdlSharp node: see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md).

The aim is PowerShell "magic" along these lines:

- `$node.Foo = 'bar'` sets property `Foo`, adding it if it doesn't exist yet. The same goes for `$node.Arg3 = x`.
- `$node['foo'] = x` and `$node[0] = x` set a property and an argument. Reading through `$node['foo']` and `$node[0]` doesn't work yet either.
- `$node += @(...)` adds arguments, properties or children.

## The model

The element list, which `RawNodeElements` already is, becomes the single source of truth: the name, then arguments and properties interleaved, duplicates included. Each element holds either:

- the original KdlSharp value, untouched, so that writing keeps its exact form (`0x10`, `1.0e-10`, raw strings, the annotation's spelling), or
- a .NET value that was set, which is converted when written, as `New-KdlNode`'s values are now.

`NodeArguments`, `NodeProperties` and the convenience members become live views of the list, and `Write-KdlNode` builds its output from it.

## Members that appear on assignment

A custom `PSPropertyAdapter`, registered for the node type with `Update-TypeData -TypeAdapter`, can do it. Probed on PowerShell 7.6:

- If the adapter's `GetProperty` returns a property for any name, assigning to a new name reaches the adapter's setter. `Get-Member`, `Select-Object`, `Format-List`, `ConvertTo-Json` and tab completion all see properties added that way. But a name claimed that way hides the type's own .NET members: `$node.Method()` then fails with "does not contain a method", and a .NET property reads as null.
- If `GetProperty` returns null for unknown names, methods and strict mode behave, but assigning to a new name throws "cannot be found".
- Claiming every name except the type's own .NET members (methods and properties) gets everything above working. The one gap is that reading a name that doesn't exist gives null even under `Set-StrictMode`, since the adapter can't tell strict mode is on, so strict mode no longer catches a misspelt name when reading.

With an adapter, `ConvertTo-Json` and `PSObject.Properties` show the type's own .NET properties as well as the adapter's.

`DynamicObject` is the other mechanism: it's how `ExpandoObject` supports `$e.Foo = 1`. Its members are invisible to `Get-Member`, `Select-Object` and tab completion, though.

The adapter also replaces the convenience members attached as note properties, which would otherwise go stale on the first edit.

## Indexers

- `this[int]` and `this[string]` indexers give `$node[0]` and `$node['foo']`, for reading and writing. PowerShell picks one by the argument's type.
- Negative indexes (`$node[-1]`) don't come with a custom indexer; it would have to implement them. Slices (`$node[0..2]`) are unprobed.
- The node mustn't implement `IEnumerable` or `IList`, or PowerShell unrolls it in the pipeline: `$node | Write-KdlNode` would send its arguments instead of the node.

## `+` and `+=`

PowerShell 7.6 doesn't use C# 14's user-defined compound assignment operators. `$x += y` is always `$x = $x + y` through `op_Addition`, even when the type has `op_AdditionAssignment`, and a type with only `+=` fails. Probed. Nothing in PowerShell/PowerShell's issues asks for support.

What `+` adds: a key-value pair becomes a property, a `KdlNode` becomes a child, an enumerable adds each of its items as whatever it is, and anything else becomes an argument. So a dictionary adds its entries as properties.

- A `DictionaryEntry` is a key-value pair too: it's what `Hashtable` and `[ordered]@{}` enumerate.
- A `string` is one value, not an enumerable of characters.
- A `byte[]` is one value, written as `(base64)`. A list of bytes as arguments is a `List<byte>` or `object[]`.

## Decided

- **`ArgN` and indexes past the end:** an error if it would leave a gap. Setting the next one (`Arg3` on a node with two arguments) appends.
- **Duplicate keys (tentative):** setting a property removes any earlier occurrences of the key.
- **`$node.Name = 'x'`** renames the node, unless the node has a `name` property.

## Open

- **Whether `+` returns a new node or modifies the node and returns it.** A new node isn't seen through other references to the old one, such as its parent's `ChildNodes`. Modifying means `$b = $a + 1` changes `$a`.
- **Spelling of new keys, and the convenience member rule.** When `$node.FirstName = 'x'` matches an existing key, such as `first-name`, it sets that one. When nothing matches, should the new key be `FirstName` as written, or KDL's usual kebab case, `first-name`? The two rules should agree. If `$node.FooBar` would create `foo-bar`, then `foo-bar` should also be the key that wins when several map to the member `FooBar`. Today's rule gives precedence to a key already spelled in PascalCase (see [kdl-powershell-module.md](kdl-powershell-module.md)).
- **Child nodes:** `ChildNodes` needs the same treatment (adding, removing, reordering). Whether `$node.child` should find a child by name is undecided: it's appealing, but it competes with properties.
- **Order of work.** Suggested: the model, then indexers for reading, then the adapter, then writing through all of them, then `+` and children.
