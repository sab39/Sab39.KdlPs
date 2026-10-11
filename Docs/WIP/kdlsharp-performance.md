# KdlSharp performance

The aim is for the KdlSharp `kdlsharp-parsing-fixes` branch (see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md)) not to regress KdlSharp's own benchmarks (`KdlSharp.Benchmarks`) against upstream, as far as possible. The branch doesn't change the benchmarks project, so both sides run identical benchmark code.

## On the branch

This is what goes in the initial PR or PRs. Any of it is open to change if the maintainer prefers otherwise.

- **Unannotated `#true`, `#false` and `#null` are the shared singletons when `PreserveSourceText` is off**, with no `SourcePosition`, as upstream has them. Upstream did this on purpose. With `PreserveSourceText`, each is a fresh instance with its own position. Switching to either behaviour, or to a separate setting, is a one-condition change in `KdlParser.ParseValue`.
- **A string token's `SourcePosition` is only created once it's known to be an argument.** A property key only gets a position with `PreserveSourceText`, for its source text.
- **Source text costs nothing in default mode.** Nodes, values and properties are exactly upstream's size:
  - `SourcePosition` is unsealed. With `PreserveSourceText`, an element's `SourcePosition` is an internal sealed subclass, `ExtendedSourcePosition`, which also holds the source string and the position of the element's type annotation. It doesn't override `ToString`.
  - `SourceText` is a public `readonly struct` wrapping an `ExtendedSourcePosition`, and every property that gives one is typed `SourceText?`, null when there's no source text. None is stored: each is created when asked for, and spans such as `FullSourceText` and a property's `SourceText` are worked out from the positions. The raw text isn't cached.
  - `KdlProperty` is unsealed. With `PreserveSourceText`, the parser creates an internal sealed subclass, `ParsedKdlProperty`, which carries the key's position. So a property constructed from a parsed value never reports a key's source text. Unsealing lets outside code subclass `KdlProperty` too, which widens the API a little.
  - **Open:** `KdlAnnotation` has an internal field for its own position, so every annotation is 8 bytes bigger in either mode. That was Claude's choice for getting the plan measured; nothing better has been discussed. The annotation object doesn't know which element it belongs to, so it can't reach that element's extended position.
- **Checking whether a string can be bare is one pass:** ASCII is looked up in a 128-bit table built from `IsIdentifierContinue`, other characters are checked individually, and surrogate pairs are checked in the same loop. In a micro-benchmark it was 3–7 times faster than the previous check on every string tried. `GeneratedRegex` isn't an option: its source generator only produces code for .NET 7 or later, and KdlSharp targets `netstandard2.1`. On .NET 10 it was also slower than the old check on short strings. An `IndexOfAny` fast reject over a span of characters was slower than none, since it rebuilds its lookup on every call. `SearchValues`, which avoids that, isn't in `netstandard2.1`.
- **Quoted strings are written straight to the `TextWriter`.** Only the characters that have to be are escaped, and the text between escapes is written a run at a time as a `ReadOnlySpan<char>`, so a string with nothing to escape is three writes and no allocation. Escaping starts from where the identifier check stopped, since every character that needs escaping also rules out a bare string, so no character is checked twice.
- **`new KdlNumber(decimal)` formats its text only when it's first needed.**

## Against upstream

In default mode:

From a full run of every benchmark, with upstream run before and after:

- **Nothing is slower than upstream beyond noise.** Query and schema benchmarks, whose code the branch doesn't change, land between −3% and +7%, the same spread as upstream's own two runs.
- **Allocations are at or below upstream's on every benchmark**, except `new KdlNumber(decimal)`, which is 16 bytes bigger for `KdlNumber`'s cached-text fields (see Later).
- **Writing is well ahead of upstream:** `html-standard` −24% and −33% (allocations −48% and −55%), string values that are valid identifiers −48%, strings that need quotes −46% (−60% with `PreferIdentifierStrings` off, as upstream effectively writes values), numbers −62%, POCO serializing −9 to −10%.
- **Parsing is level or ahead:** `Parse1KB` −12%, `ParseComplex` −6 to −8%, `ParseSimple` −8%, `html-standard` +1%, all-keyword input level, POCO deserializing within noise. `Lexer.Advance` handles printable ASCII with a single range test.

With `PreserveSourceText`, parsing costs about +7–9% time and +6–14% allocations over upstream on ordinary documents, and +18% and +33% on all-keyword input, where every keyword gets its own instance.

## Later

Measure whether `KdlNumber`'s two cached-text fields (`canonicalText` and `rawText`) are each worth their 8 bytes, or whether one or both should be computed when asked for.

## Measuring

- Compare only within one benchmark session. Between sessions, the same upstream benchmark has moved by over 20%, and in-session noise is around ±3–5%, more on benchmarks of a few nanoseconds. Allocation counts are exact, so any allocation difference is real.
- The machine must be kept awake for the whole run (see the global CLAUDE.md).
- Anything an experiment adds to every parse shows up on the small-document benchmarks. Reading an environment variable in `KdlParserSettings`' constructor added about 100 ns per parse, which made `ParseSimple` look 18% slower.

## Ideas

None of these is decided.

- **`SourcePosition` as a struct** (Stuart's idea). Upstream's class costs 40 bytes per element: a 32-byte object plus an 8-byte reference to it. A struct with four ints, `Length` included, is 20 bytes inline as `SourcePosition?`, with no object. That could put default-mode parsing below upstream's allocations. It's a binary break: every property typed `SourcePosition` becomes `Nullable<SourcePosition>`, so under SemVer it needs a major version. Most source still compiles (`?.`, `== null` and `is { }` all work the same), but code that null-checks and then uses the value directly needs `.Value`, and equality goes from comparing references to comparing values. Claude's view is that it's best offered as a PR of its own, so the fixes don't depend on it.
- **What a reference can do that an inline struct can't** (Stuart's point). Default mode only needs one position per element. Preserve mode needs more: key and annotation positions, plus the source string. A reference can point to one object holding all of that, which is how `ExtendedSourcePosition` works. An inline struct can't share its storage with a reference, since overlapping reference and non-reference fields isn't allowed. Combining a struct with preserve-mode extras means an extra reference field, which is null in default mode (28 bytes inline in all), or a side table used only in preserve mode.
- **Generic node and value types over a struct type parameter**, sized for whether source text is kept (Stuart's idea, which he thinks may be more complex than it's worth). Claude's view is that the cost is high: the generic parameter would spread through the whole public API, and the value types are public and sealed.
- **Returning number text that's already canonical as is** (Claude's suggestion). `NumberParser.ToCanonical` allocates about 140 bytes per number written (substrings and a `StringBuilder`), even when the text is already canonical.
