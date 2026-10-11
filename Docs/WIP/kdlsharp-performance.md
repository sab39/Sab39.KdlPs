# KdlSharp performance

The aim is for the KdlSharp `kdlsharp-parsing-fixes` branch (see [kdlsharp-parsing-fixes.md](kdlsharp-parsing-fixes.md)) not to regress KdlSharp's own benchmarks (`KdlSharp.Benchmarks`) against upstream, as far as possible. The branch doesn't change the benchmarks project, so both sides run identical benchmark code.

Nothing has been decided about any of the costs below yet. Understanding where they come from comes first.

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

- **Boolean singletons when `PreserveSourceText` is off** (Stuart's idea). This saves the 88 bytes per keyword, but unannotated keywords would go back to having no `SourcePosition` in default mode, unlike every other value. Giving them one was a fix on the branch (`764063d`).
- **One storage field for position and source texts** (Stuart's idea, worked through with Claude). The `SourceText` setters are all `internal`, so the storage can change without changing the public API. A single private field could hold a `SourcePosition` in default mode. In preserve mode it could hold a `SourceText` (whose `Position` is the element's position), or a holder with more than one source text only where an element needs more. Default mode would then be exactly upstream's size. Preserve mode would also stop allocating each position twice, once as `SourcePosition` and once inside `SourceText`.
- **Generic node and value types over a struct type parameter**, sized for whether source text is kept (Stuart's idea, which he thinks may be more complex than it's worth). Claude's view is that the cost is high: the generic parameter would spread through the whole public API, and the value types are public and sealed.
- **Creating a string token's `SourcePosition` only once it's known to be an argument** (Stuart's idea). `Token` keeps its line, column, offset and length, so the position can be created after the next token has been read, in either mode.
- **Faster quotability checking** (Stuart's idea). KdlSharp targets only `netstandard2.1`, so `SearchValues` and `IndexOfAnyExcept` aren't available without adding a target framework. `MemoryExtensions.IndexOfAny(ReadOnlySpan<char>)` is, and on a modern runtime it runs the runtime's own vectorized implementation, which could reject ASCII characters that need quoting quickly.
- **Checking and escaping in one pass, written straight to the `TextWriter`** (Claude's suggestion). `StringEscaper.Escape` builds a `StringBuilder` and a string for every quoted string, upstream included.
- **Formatting a constructed number's text only when first needed**, and **returning number text that's already canonical as is** (both Claude's suggestions).
