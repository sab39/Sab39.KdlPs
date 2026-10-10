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

## Links

- [kdl.dev](https://kdl.dev/): spec, implementation list, tooling
- [kdl-org/kdl](https://github.com/kdl-org/kdl): spec repo and its licence
- [KdlSharp on NuGet](https://www.nuget.org/packages/KdlSharp): source at [AndreyAkinshin/KdlSharp](https://github.com/AndreyAkinshin/KdlSharp)
