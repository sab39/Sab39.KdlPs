# KdlSharp performance

The aim is for the KdlSharp `kdlsharp-parsing-fixes` branch (see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md)) not to regress KdlSharp's own benchmarks (`KdlSharp.Benchmarks`) against upstream, as far as possible. The branch doesn't change the benchmarks project, so both sides run identical benchmark code.

## Decided

- **Unannotated `#true`, `#false` and `#null` are the shared singletons when `PreserveSourceText` is off**, with no `SourcePosition`, as upstream has them. Upstream did this on purpose. With `PreserveSourceText`, they stay fresh instances with a position. This is what goes in the initial PR or PRs, and it's open to change if the maintainer prefers otherwise. It should be coded so that switching to either behaviour, or to a separate setting, is a small change.
- **A string token's `SourcePosition` is only created once it's known to be an argument**, in both modes. A property key's position is only needed with `PreserveSourceText`, for its source text.
- **`SourceText` is a public `readonly struct`**, and every property that gives one is typed `SourceText?`. It's null when there's no source text, rather than a struct holding a null reference. `SourceText` is new API, so making it a struct breaks nothing. It has its own `ToString` (the raw text) and the public API (`RawText`, `AsSpan()`, `Position`).

## Tentative

### Where source text is stored

Nodes and values in default mode would be exactly upstream's size, with no new fields:

- **`SourcePosition` is unsealed**, which isn't a breaking change. In preserve mode, an element's existing `SourcePosition` property holds an internal sealed subclass, `ExtendedSourcePosition`. It holds the source string and anything else preserve mode needs, such as where the element's type annotation starts. In default mode, the property holds a plain `SourcePosition` as upstream does. The subclass doesn't override `ToString`, so `SourcePosition.ToString()` reads the same in both modes.
- **`SourceText` wraps an `ExtendedSourcePosition`** and is created when asked for: `SourceText`, `FullSourceText` and the like test the element's `SourcePosition` for the subclass, so no `SourceText` is stored. The raw text isn't cached.
- **Spans are worked out when asked for:** `FullSourceText` from the annotation start to the end of the element, and a property's `SourceText` from the key to the value.
- **Property keys:** `KdlProperty` is unsealed, and the parser creates an internal sealed subclass carrying the key's extended position, in preserve mode only. That costs nothing in default mode, and a constructed property never reports a parsed key's source text, even when its value was parsed. Unsealing lets outside code subclass `KdlProperty` too. It isn't a breaking change, but it widens the API a little.

Open within this:

- **`KdlAnnotation.SourceText`:** the annotation object doesn't know which element it belongs to, so it can't reach that element's extended position.

### Fast reject when checking whether a string can be bare

Strings that need quotes should be rejected by a fast first pass before the full identifier check. The approach is open. `GeneratedRegex` isn't available, because the regex source generator only produces code for .NET 7 or later and KdlSharp targets only `netstandard2.1`. Its output for an equivalent regex on .NET 10 or 11 may still suggest how to make the check fast.

## Later

After the improvements we're already confident of are done: measure whether `KdlNumber`'s two cached-text fields (`canonicalText` and `rawText`) are each worth their 8 bytes, or whether one or both should be computed when asked for.

## Measuring

- Compare only within one benchmark session. Between sessions, the same upstream benchmark has moved by over 20%, and in-session noise is around ±3–5%. Allocation counts are exact, so any allocation difference is real.
- The machine must be kept awake for the whole run (see the global CLAUDE.md).

## What costs what

Against upstream, in default mode (without `PreserveSourceText`):

**Parsing: about +5–8% time and +6–13% allocations.**

- Each node, value and property is 16 bytes bigger, for its two `SourceText` reference fields, which are null unless `PreserveSourceText` is on.
- Each property allocates a `SourcePosition` it doesn't use: the parser creates one for every string token before it knows whether the token is a key or an argument.
- Each unannotated `#true`, `#false` and `#null` allocates 88 bytes: a fresh instance with its own `SourcePosition`, where upstream returned a shared singleton with no position. Parsing a document that's all keywords is +14% time and +33% allocations, almost all from this.
- With all three undone, parse allocations match upstream exactly. Parse time matches upstream on `html-standard` and on POCO deserializing. The field overhead accounts for about 3 points of the extra parse time, and the keyword instances for most of the rest on keyword-heavy input. The unused key `SourcePosition` is only an allocation cost: its time effect is within noise.
- **Open:** documents made of short lines of identifiers and keywords (`ParseComplex`, `ParseBooleans`) still parse about 2–3% slower with all three undone. That's close to the noise floor. It looks like a per-token cost in the lexer (`NextToken` setting `Token.Length`, or `StringEscaper.LooksLikeNumber`), but it hasn't been pinned down.

**Writing numbers is faster than upstream:** about −58% time and −45% allocations. `NumberParser.ToCanonical` still allocates about 140 bytes per number written (substrings and a `StringBuilder`), even when the text is already canonical. Returning such text as is gets that to −64%.

**Writing strings:** with `PreferIdentifierStrings` (the default), string values that are valid identifiers are now written bare (bug 8). For those, the identifier check costs about what skipping the escape saves: time is unchanged and allocations drop by 70%. Strings that need quotes pay for both the check and the escape: +42% time. On the official `html-standard` benchmark, which mixes both, allocations are down about 20%. The time change there is within session-to-session noise.

**`new KdlNumber(decimal)`:** 3.3 ns and 88 bytes upstream, 56 ns and 352 bytes now, because the constructor formats the number's text eagerly (`NumberParser.FormatConstructed`). Formatting it when first needed brings it back to about 4 ns. The POCO serializer creates numbers this way.

**POCO serializer (`SerializationBenchmarks`): about +8–10% in both directions.** These are small benchmarks with several contributors (number construction, the string check, the parse overheads). Undoing all the above together roughly halves it.

## Ideas

None of these is decided.

- **`SourcePosition` as a struct** (Stuart's idea). Upstream's class costs 40 bytes per element: a 32-byte object plus an 8-byte reference to it. A struct with four ints, `Length` included, is 20 bytes inline as `SourcePosition?`, with no object. That could put default-mode parsing below upstream's allocations. It's a binary break: every property typed `SourcePosition` becomes `Nullable<SourcePosition>`, so under SemVer it needs a major version. Most source still compiles (`?.`, `== null` and `is { }` all work the same), but code that null-checks and then uses the value directly needs `.Value`, and equality goes from comparing references to comparing values. Claude's view is that it's best offered as a PR of its own, so the fixes don't depend on it. A reference to `SourceText` inside the struct would make it 24–32 bytes inline even in default mode.
- **What a reference can do that an inline struct can't** (Stuart's point). Default mode only needs one position per element: upstream stores no position for property keys or type annotations, and the decided singletons store none for keywords. Preserve mode needs more: key and annotation positions, plus the source string. A reference can point to one object holding all of that, so a single field can serve both modes. An inline struct can't share its storage with a reference, since overlapping reference and non-reference fields isn't allowed. Combining a struct with preserve-mode extras means an extra reference field, which is null in default mode (28 bytes inline in all), or a side table used only in preserve mode.
- **Generic node and value types over a struct type parameter**, sized for whether source text is kept (Stuart's idea, which he thinks may be more complex than it's worth). Claude's view is that the cost is high: the generic parameter would spread through the whole public API, and the value types are public and sealed.
- **`MemoryExtensions.IndexOfAny(ReadOnlySpan<char>)` for the fast reject** (Claude's suggestion). It's available in `netstandard2.1`, and on a modern runtime it runs the runtime's own vectorized implementation. `SearchValues` and `IndexOfAnyExcept` aren't available without adding a target framework.
- **Checking and escaping in one pass, written straight to the `TextWriter`** (Claude's suggestion). `StringEscaper.Escape` builds a `StringBuilder` and a string for every quoted string, upstream included.
- **Formatting a constructed number's text only when first needed**, and **returning number text that's already canonical as is** (both Claude's suggestions).
