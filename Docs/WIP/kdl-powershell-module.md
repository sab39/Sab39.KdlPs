# KDL PowerShell module

An experiment in building a PowerShell module for reading and writing [KDL](https://kdl.dev/), using KdlSharp as the parser.

## Background

This grew out of looking for a general-purpose file format suited to content that is effectively source code. YAML beats JSON, and JSON beats XML, for that purpose. None of the three has a "space-separated list of tokens" form, though. KDL does, because it is node-based: each node is a name, then space-separated arguments, optional `key=value` properties, and an optional `{ }` block of children:

```kdl
var {
    x int 1
    y bool #true
    z string #null
}
```

Here `x` is a node whose arguments are `["int", 1]`. In KDL v2, bare identifiers are valid string values, and the literals are spelled `#true` / `#false` / `#null`.

## Governance and licensing

- KDL is a community project maintained by the kdl-org GitHub organisation. No standards body is involved. The spec repo contains a document in IETF draft format, but it hasn't been submitted to the IETF, and there is no registered media type.
- The current spec is KDL 2.0.0, which the maintainers have marked as finalized with no further changes expected.
- The spec text is licensed CC BY-SA 4.0. The spec repo states explicitly that implementations are not derivative works, so share-alike doesn't apply to parsers or to programs that use KDL. It would only apply if the spec text itself were copied.
- KdlSharp is MIT-licensed.

## Ecosystem

- **KdlSharp** (Andrey Akinshin, first released December 2025). Supports KDL v2, with a v1 compatibility mode. Provides a document model API, POCO serialisation, and the KDL query and schema languages. No dependencies; targets .NET Standard 2.1. Serialising an object graph that contains cycles throws.
- Other .NET packages: Kadlet supports v1 only. The remaining packages on NuGet are alpha or abandoned.
- Parsers exist in about 20 other languages. The Rust crate `kdl-rs` is the flagship and includes `kdl-lsp`, a language server.
- Editor support covers VS Code, IntelliJ, Sublime, vim, Kate, Zed and Nova, plus a tree-sitter grammar.
- Other tooling: several formatters (`kdlfmt`, `kdl-fmt`, `kdlf`), a query language based on CSS selectors, and a schema language modelled on JSON Schema.
- Some of the ecosystem still targets v1, which differs from v2 in ways that matter here (bare-identifier strings, `#`-prefixed literals). v2 support has to be checked per tool.

## Relevant spec rules

- Argument order is significant, but only relative to other arguments. Arguments and properties can be interleaved in any order, and the interleaving carries no meaning.
- Property order is not significant. Duplicate property names are legal, and the rightmost one wins.
- Child order is significant. Duplicate child node names are not forbidden.
- Type annotations, such as `(date)node` or `(u8)255`, can appear on nodes and on values. The spec reserves a set of value annotations, listed below. Parsers MAY recognise them, and if they do, they SHOULD interpret them as the spec defines. Implementations are free to ignore them. No node annotations are reserved.
  - Integers: `i8` to `i128`, `u8` to `u128`, `isize`, `usize`.
  - Decimal numbers: `f32`, `f64`, `decimal64`, `decimal128`.
  - Strings: `date-time`, `time`, `date`, `duration`, `decimal`, `currency`, `country-2`, `country-3`, `country-subdivision`, `email`, `idn-email`, `hostname`, `idn-hostname`, `ipv4`, `ipv6`, `url`, `url-reference`, `irl`, `irl-reference`, `url-template`, `uuid`, `regex`, `base64`, `base85`.

## Parameters of `Get-KdlNode`

- **Input:** one of `-Path` (wildcards, positional, from the pipeline), `-LiteralPath` (aliased `PSPath`, so `Get-ChildItem` output binds), or `-Text` (KDL source as a string).
- **`-Query`:** a KDL query, executed by KdlSharp. Only the nodes it matches are output. An invalid query is a terminating error.
- **`-Recurse`:** outputs each node, then all its descendants, depth-first. If a query matches both a node and one of its descendants, the descendant is output twice.

## Output of `Get-KdlNode`

Each node becomes an object with the members below. The fixed members are prefixed with `Node` so that they don't collide with properties that are likely in real documents, such as `name=`.

- `NodeName`
- `NodeArguments`: a list of converted values.
- `NodeProperties`: a case-sensitive ordered dictionary of converted values. When a key is duplicated, the last value wins, as the spec requires. This is where exact property names live.
- `ChildNodes`: a list of the same kind of object.
- `RawNodeElements`: object forms of every element of the node, in source order. Each carries its type annotation, source position and so on.
  - `[0]` is the node name itself, so its annotation is captured too.
  - Arguments and properties follow, interleaved as written. Property entries carry their key, and overridden duplicates are kept.
  - The interleaving is best effort, because of the keyword-position gap in KdlSharp (see KdlSharp below). That gap is to be fixed in the KdlSharp patch or fork, along with the raw text. Tests for the correct behaviour already exist and are marked as expected failures.
- Generated convenience members for the arguments:
  - `Arg1`, `Arg2` and so on, one per argument.
  - `Argument` as well, when the node has exactly one argument.

  These come before the property members. A KDL property whose member name clashes with one of them wins, the same way a real `name` property wins over the `Name` alias.
- A generated convenience member for each KDL property. These are a convenience only; exact values come from `NodeProperties`.
  - **Names:** KDL property names are converted to PascalCase, so `first-name`, `first_name` and `firstName` all become `FirstName`.
  - **Clashes:** when several KDL names convert to the same member name, a key that is already exactly that PascalCase name wins. Otherwise the first one in source order wins.
  - **Duplicates:** a literal duplicate key (`a=1 a=2`) is not a clash. The member gets the last value, so it agrees with `NodeProperties`.

### Node annotations

The spec allows two readings of an annotation on a node name: a suggested type for the name string itself, or an elaboration of what kind of node it is. The API stays neutral between them. The annotation is captured on `RawNodeElements[0]`, and the consumer decides what it means.

### Value conversion

- **Bare numbers** are typed by how the literal is written:
  - Integer literals, meaning no `.` and no exponent (including hex, octal and binary), become `int`, then `long`, then `BigInteger`, whichever is the first to fit.
  - Literals with a `.` or an exponent become `decimal` when the value is within `decimal`'s range and doesn't underflow to zero. Otherwise they become `double`.
  - Numbers are parsed from the source text, not from KdlSharp's `decimal` value. Parsing reads a number starting at the value's `SourcePosition`, then checks that a valid delimiter follows where the number ends. This is internal only and is never exposed as raw text.
- **Annotated values** use the most specific suitable .NET type, including a dedicated type wherever one exists that the mapping below overlooks:

  | Annotation | .NET type |
  |---|---|
  | `i8`, `i16`, `i32`, `i64`, `i128` | `sbyte`, `short`, `int`, `long`, `Int128` |
  | `u8`, `u16`, `u32`, `u64`, `u128` | `byte`, `ushort`, `uint`, `ulong`, `UInt128` |
  | `isize`, `usize` | `nint`, `nuint` |
  | `f32`, `f64` | `float`, `double` |
  | `decimal64`, `decimal128` | `decimal` (not an exact fit: these are IEEE decimal formats) |
  | `date-time`, `date`, `time`, `duration` | `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`. Durations are ISO 8601, which `TimeSpan` doesn't parse directly. |
  | `decimal` (string) | `decimal` |
  | `email`, `idn-email` | `MailAddress`. It also accepts a display-name form such as `Name <a@b.c>`. |
  | `country-2` | `RegionInfo` |
  | `ipv4`, `ipv6` | `IPAddress` |
  | `url`, `url-reference`, `irl`, `irl-reference` | `Uri` |
  | `uuid` | `Guid` |
  | `regex` | `Regex` |
  | `base64`, `base85` | `byte[]` |

  No dedicated .NET type was found for `currency`, `country-3`, `country-subdivision`, `hostname`, `idn-hostname` or `url-template`.
- **Numbers too large for KdlSharp** fail the whole parse (see KdlSharp below). For now, that has to surface as a proper PowerShell error. It's another candidate for the KdlSharp patch or fork.
- **Unmapped or unconvertible values.** For a first pass (which may be revisited), the rules are:
  - **Strings:** an unknown annotation, a numeric annotation, or a failed conversion leaves the value as a string and emits a warning.
  - **Numbers:** the same cases fall back to the unannotated number conversion, with a warning. Falling back to a string would mean exposing raw text.
  - **Reserved string types with no .NET type**, such as `hostname`, map to string and don't warn.
  - **Booleans and nulls** with any annotation warn.
- **Node-name annotations never warn.** Under the spec's "elaboration" reading, annotations like `(contributor)person` are normal. `RawNodeElements[0].Value` is converted when a reserved string mapping succeeds. `NodeName` is always the plain string.

### Member clashes and `Name`

- A generated member that PowerShell won't allow is skipped, and the property stays reachable through `NodeProperties`. This covers clashes with members every object already has, such as `GetType` and `PSObject`, and case-insensitive clashes with the fixed members, such as `nodename=`.
- `Name` is an alias for `NodeName` on any node with no case variant of `name` among its properties.

### Types

The output object's type is `KdlNode`. Its default view is a table with these columns: `NodeName`, `Arguments`, `Properties` and `ChildNodes`. A fixed view can't add a column for each generated member, so `Properties` renders `NodeProperties` as `key=value` pairs instead.

`RawNodeElements` holds `KdlElement` objects, with these members:

- `Kind`: an enum with the values `NodeName`, `Argument` and `Property`.
- `Key`: the property key. `null` for anything that isn't a property.
- `Value`: the converted value.
- `SourcePosition`: KdlSharp's own type. It points at the start of the value, after any key or annotation. It is null for unannotated `#true`, `#false` and `#null`, because KdlSharp gives those no position.
- `Annotation`: the element's type annotation.
- Possibly other members, if any turn out to be missing.

`KdlElement` has no raw source text yet. That is not acceptable long term, but a member that is sometimes wrong is worse than none. KdlSharp gives only a start position for values, with nothing for keys, annotations or the end of a value (see KdlSharp below). Working around that from outside the library is fragile. The plan is to fix KdlSharp itself later, by PR or by fork, and then add the raw text. The same fix should give `#true`, `#false` and `#null` source positions, so that `RawNodeElements` interleaving stops being best effort.

## KdlSharp (1.1.0)

Version 1.1.0 is the one tested. The plan is to fix the bugs and limitations below later, by PR or by fork. Everything listed was observed directly.

### Bugs

These are behaviours that are wrong by the spec, or silently wrong.

1. **Numbers above `decimal.MaxValue` (about 7.9e28) fail the whole document**, if they are within `double`'s range. That covers integers and exponent forms alike, for example `340282366920938463463374607431768211456` and `1e29` up to `1e308`. Both the document API and `KdlReader` fail. So `u128`/`i128` values near the top of their range, and large `f64` values, can't be read through KdlSharp at all. In this module the failure surfaces as a parse error for the file.
2. **Numbers beyond `double`'s range saturate silently.** `1e309` and `1e400` become `decimal.MaxValue` instead of failing.
3. **Numbers too small for `decimal` underflow silently.** `1e-30` and `1e-400` become `0`.
4. **Precision beyond about 28 significant digits is rounded silently** in `KdlNumber.Value`.
5. **Negative hex, octal and binary have wrong values.** `-0x10` parses, but reading its value (`KdlNumber.Value`, or `KdlReader.NumberValue`) throws `FormatException`. Negative octal and binary are silently wrong: `-0o20` gives -176, and `-0b10000` gives -80. The `RawText` of all three puts the sign after the prefix, for example `0x-10`.
6. **Unannotated `#true`, `#false` and `#null` have no `SourcePosition`.** With a type annotation, such as `(tag)#true`, they do have one. Every other value has one, including `#inf`, `#-inf` and `#nan`. Arguments and properties are kept in separate lists, so these keywords' place relative to the *other* list can't be recovered. This is why `RawNodeElements` interleaving is best effort, and three tests marked as expected failures cover it.
7. **`top() > package` matched nothing** against a document whose only top-level node is `package`. This is probably a query bug, but the query spec's definition of `top()` hasn't been checked.

### Limitations

These are missing capabilities rather than wrong results.

1. **`SourcePosition` records only the start** (`Line`, `Column`, `Offset`), with nothing for where an element ends. `KdlReader` exposes only its current position, not token boundaries. So exact source text can't be sliced out for strings, and this module has no raw-text member.
2. **No positions for keys or annotations.** A value's offset points past its annotation, for node names, arguments and property values alike, and a property's key has no position at all.
3. **An annotation's source form is lost.** `TypeName` is decoded, so `( u8 )` and `(u8)` both give `u8`, and a quoted annotation such as `("my type")` gives `my type`.
4. **`KdlNumber.RawText` is set only for hex, octal and binary literals.**
5. **`KdlReader`'s number `RawText` isn't raw:** underscores are stripped (`1_000` gives `1000`). Its string tokens don't record bare, quoted or raw form.

### Behaviour this module relies on

- Type annotations are never interpreted. Nodes and values expose them only as `TypeAnnotation.TypeName`. Values are only `KdlString`, `KdlNumber`, `KdlBoolean` and `KdlNull`, so all type mapping is up to this module.
- `KdlString.Value` is the decoded string, and `StringType` records bare, quoted, raw or multi-line.
- `#inf`, `#-inf` and `#nan` show up through flags such as `IsPositiveInfinity`, not through `Value`.
- `SourcePosition.Offset` is a UTF-16 offset into the source text. Reading a number from it up to the next delimiter gives the exact original text, underscores included. That's how this module gets correct number values despite bugs 2 to 5 above.
- Duplicate properties are kept in `Properties`, in source order. `AllowDuplicateProperties` defaults to true, and the parser defaults to KDL v2.
- Queries (`KdlQuery.Compile`) handle child (`>`), descendant (`>>`), alternative (`||`), property and `val()` matchers, and `[]` for any node.

### Contributing

KdlSharp has no `CONTRIBUTING.md`. The README points to `AGENTS.md`, which it calls the development and contributing guide, and that file is addressed to AI agents as much as to people. It requires:

- builds with warnings as errors and full analyzers, and `dotnet format --verify-no-changes` passing
- XML docs on all public APIs
- no dependencies in the core library, which stays on .NET Standard 2.1
- exact compliance with the KDL v2 spec, including passing the official test suite (a git submodule)
- tests for edge cases the official suite doesn't cover

`AGENTS.md` says every official test passes, so the bugs above are presumably outside that suite. Neither the repo nor Anthropic's consumer terms restrict contributing AI-assisted code to it.

## Links

- [kdl.dev](https://kdl.dev/): spec, implementation list, tooling
- [kdl-org/kdl](https://github.com/kdl-org/kdl): spec repo and its licence
- [KdlSharp on NuGet](https://www.nuget.org/packages/KdlSharp): source at [AndreyAkinshin/KdlSharp](https://github.com/AndreyAkinshin/KdlSharp)
