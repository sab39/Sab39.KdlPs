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

- **Input:** one of `-Path` (wildcards, positional, from the pipeline), `-LiteralPath` (aliased `PSPath`, so `Get-ChildItem` output binds), or `-Text` (KDL source as a string, where empty text is an empty document).
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
  - Literals beyond `double`'s range are a gap: `1.23e1000` becomes infinity and `1.23e-1000` becomes `0`, with no warning.
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

## `New-KdlNode`

Creates a node of the same type `Get-KdlNode` outputs, with the same generated members.

- **`-NodeName`:** positional.
- **`-NodeArguments`:** takes the remaining arguments, so `New-KdlNode foo bar baz` is `New-KdlNode -NodeName foo -NodeArguments bar, baz`. A remaining argument that starts with `-`, such as `-2`, arrives as a string, because PowerShell takes it for a parameter name. `(-2)` gives the number.
- **`-NodeProperties`:** a hashtable, an ordered dictionary or a custom object. Anything else is a terminating error.
- **`-ChildNodes`:** nodes from either cmdlet.

`RawNodeElements` is the name, then the arguments, then the properties, with no annotations or source positions.

## `Write-KdlNode`

- Everything piped in becomes one document, output as a single string once the pipeline ends, with no trailing newline. That string's line terminator is the one PowerShell adds to every string it outputs. With no input, the output is an empty string, because an empty document is still a document.
- **`-Normalize`** writes the normalized form that the official test suite's expected files use. For now, its only effect is to sort properties by key, ordinally. The spec gives property order no meaning, so without `-Normalize` properties are written in `NodeProperties` order.
- KdlSharp does the serialization, with its default formatter settings except for `PreserveStringTypes`. Its shortcomings are to be fixed in KdlSharp itself (see KdlSharp below). Until then, there are things `Get-KdlNode` can read that can't be written.
- Strings are written as bare identifiers wherever KdlSharp considers that valid. The exception is a string that starts with a digit, or with a sign or `.` followed by a digit, which is quoted (bug 9).
- The node-name annotation from `RawNodeElements[0]` is written. Value annotations aren't.
- Anything that can't be written yet is a terminating `KdlWriteNotSupported` error, not wrong output. That covers:
  - values other than strings, booleans, null and numbers
  - numbers outside `decimal`'s range (limitation 6)
  - node names and property keys that look like numbers (bug 9).

  Bug 10 is the exception: it writes wrong numbers without an error.
- A whole-number `double` or `float` is written with a fraction, such as `1.0`, so it doesn't read back as an integer. Decimals keep their scale, so `1.50m` is written as `1.50`. PowerShell's `[decimal]'1.0'` cast drops the scale and gives `1`, which is then written as `1`.

## Testing

- Unit tests use MSTest on Microsoft.Testing.Platform. `global.json` makes `dotnet test` use that runner.
- The cmdlet tests run the cmdlets in an in-process PowerShell from `Microsoft.PowerShell.SDK`, set up by `ModuleShell`. The test project references the SDK; the module doesn't.
- Behaviour that's correct by the spec but known not to work yet, because of a KdlSharp bug or a gap in this module, is covered by tests marked as expected failures.
- **Execution policy in the hosted PowerShell.** The hosted PowerShell's `$PSHOME` is the test output's `runtimes\win\lib\net10.0` folder, which has no `powershell.config.json`. So it never sees the machine's real policy, all scopes are `Undefined`, and the Windows default of `Restricted` applies. `Restricted` silently skips loading `.ps1xml` format files. Setting `InitialSessionState.ExecutionPolicy` comes too late to prevent that, so the test sessions use the base `AuthorizationManager`, which allows everything.
- **The official KDL test suite** is the `specs` submodule ([kdl-org/kdl](https://github.com/kdl-org/kdl), as in KdlSharp). `OfficialTestSuiteTests` runs every input file as a case of two tests. Cases known not to pass yet are listed there with their reasons and run as expected failures.
  - **Read matches read:** for each input file, `Get-KdlNode` gives the same node tree as it does for the matching `expected_kdl` file. Names, annotations, arguments and children are compared in order. Properties are compared by key, since expected files sort properties and keep only the rightmost duplicate. Values must be equal and of the same type. Nothing tied to the source text is compared, such as source positions or string forms. A `_fail` input must give a `KdlParseFailed` error.
  - **Write matches expected:** piping `Get-KdlNode` output for each input into `Write-KdlNode -Normalize` reproduces the `expected_kdl` file exactly. The two are compared line by line, which is how PowerShell shows the output string. That also accepts either line ending in the expected file, since with `core.autocrlf` on, the submodule checks out with CRLF.
  - **Read, write, read matches read:** for each input, and for its expected file unless the two are identical, writing what it reads as with plain `Write-KdlNode` reads back as the same nodes, compared the same way as in "read matches read". This checks that the output parses to the same values, without prescribing its form.
  - The suite itself is meant as a parsing test. Its README says it verifies "the ability to **parse**", and that output may be produced however is needed to match `expected_kdl`. So both write tests go beyond what the suite asks for.
- **Agent shells hide execution-policy problems.** Commands run by Claude Code here inherit `PSExecutionPolicyPreference=Bypass`, which every child process, including test runs, picks up. A test that depends on execution policy can pass under an agent and fail in Visual Studio. To reproduce Visual Studio's conditions, clear that variable before running the tests.

## KdlSharp (1.1.0)

Version 1.1.0 is the one tested. The plan is to fix the bugs and limitations below later, by PR or by fork. Everything listed was observed directly. Fixes are in progress on the `kdlsharp-parsing-fixes` branch: see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md).

This doc describes the module on `main`, built against 1.1.0, including its workarounds for these bugs. On the module's `kdlsharp-parsing-fixes` branch, its code is written as if they were all fixed. That doc covers how it differs.

### Bugs

These are behaviours that are wrong by the spec, or silently wrong.

1. **Numbers above `decimal.MaxValue` (about 7.9e28) fail the whole document**, if they are within `double`'s range. That covers integers and exponent forms alike, for example `340282366920938463463374607431768211456` and `1e29` up to `1e308`. Both the document API and `KdlReader` fail. So `u128`/`i128` values near the top of their range, and large `f64` values, can't be read through KdlSharp at all. In this module the failure surfaces as a parse error for the file.
2. **Numbers beyond `double`'s range saturate silently.** `1e309` and `1e400` become `decimal.MaxValue` instead of failing.
3. **Numbers too small for `decimal` underflow silently.** `1e-30` and `1e-400` become `0`.
4. **Precision beyond about 28 significant digits is rounded silently** in `KdlNumber.Value`.
5. **Negative hex, octal and binary have wrong values.** `-0x10` parses, but reading its value (`KdlNumber.Value`, or `KdlReader.NumberValue`) throws `FormatException`. Negative octal and binary are silently wrong: `-0o20` gives -176, and `-0b10000` gives -80. The `RawText` of all three puts the sign after the prefix, for example `0x-10`.
6. **Unannotated `#true`, `#false` and `#null` have no `SourcePosition`.** With a type annotation, such as `(tag)#true`, they do have one. Every other value has one, including `#inf`, `#-inf` and `#nan`. Arguments and properties are kept in separate lists, so these keywords' place relative to the *other* list can't be recovered. This is why `RawNodeElements` interleaving is best effort, and three tests marked as expected failures cover it.
7. **`top() > package` matched nothing** against a document whose only top-level node is `package`. The query spec's definition of `top()` supports KdlSharp's reading if taken literally, but contradicts the spec's own example: see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md).
8. **`PreferIdentifierStrings` has no effect.** The formatter writes strings bare only when `PreserveStringTypes` is on and the string's `StringType` is `Identifier`.
9. **The formatter's identifier check lets number-like strings through.** With the settings above, `1x`, `-1`, `+1`, `.5` and `-.5` are all written bare, so they read back as numbers or fail to parse. Node names and property keys are written bare the same way, and since they aren't `KdlString`s, no string type can make them quoted.
10. **Numbers from 1e10 up, or below 1e-4, are written in E notation with only 7 significant digits.** For example, `long.MaxValue` gives `9.223372E+018`, and `0.000012345` gives `1.234500E-005`.
11. (This turned out not to be a KdlSharp bug.)
12. **Annotations that need quoting are written bare.** An empty annotation is written as `()`, and `"type/"` as `(type/)`.
13. **KdlSharp's tests don't cover its formatter's output.** The official test suite tests parsing, and `OfficialTestRunner` uses it that way, comparing parsed structures. That fits the suite's stated purpose, but nothing else in KdlSharp checks what the formatter writes. That's how bugs 8 to 10, 12 and 14 get past a suite that `AGENTS.md` says passes in full. On the fixes branch, `OfficialTestRunner` has a round-trip test (write, parse again, compare), which catches the ones that don't parse back to the same values.
14. **The default formatter escapes characters outside the BMP as surrogate pairs.** 😀 is written as `"\u{D83D}\u{DE00}"`, which is invalid, because `\u{}` escapes must be Unicode scalar values. KdlSharp's own parser rejects it. `Write-KdlNode` hits this only for strings that have to be quoted, such as `"a 😀"`, since it writes every other string bare.
15. **Disallowed code points are accepted inside strings and comments, and U+FEFF inside identifiers.** The spec's disallowed literal code points, such as U+200E and U+007F, are only rejected at the start of a token. So `node "a<U+200E>b"` parses. U+FEFF isn't in KdlSharp's disallowed set at all, so the formatter writes it bare in identifiers and literally in strings. Observed on the fixes branch before its fix, where that code is unchanged from 1.1.0.

### Limitations

These are missing capabilities rather than wrong results.

1. **`SourcePosition` records only the start** (`Line`, `Column`, `Offset`), with nothing for where an element ends. `KdlReader` exposes only its current position, not token boundaries. So exact source text can't be sliced out for strings, and this module has no raw-text member.
2. **No positions for keys or annotations.** A value's offset points past its annotation, for node names, arguments and property values alike, and a property's key has no position at all.
3. **An annotation's source form is lost.** `TypeName` is decoded, so `( u8 )` and `(u8)` both give `u8`, and a quoted annotation such as `("my type")` gives `my type`.
4. **`KdlNumber.RawText` is set only for hex, octal and binary literals.**
5. **`KdlReader`'s number `RawText` isn't raw:** underscores are stripped (`1_000` gives `1000`). Its string tokens don't record bare, quoted or raw form.
6. **A decimal-format `KdlNumber` can only be constructed from a `decimal`.** Integers beyond `decimal`'s range, and doubles too large or too small for it, can't be written.

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
