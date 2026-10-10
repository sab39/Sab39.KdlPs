# KdlSharp parsing fixes

Fixes for the KdlSharp bugs and limitations listed under "KdlSharp (1.1.0)" in [kdl-powershell-module.md](kdl-powershell-module.md), with this module built against the changed KdlSharp so the fixes can be tested through it. The KdlSharp changes are meant to go upstream as PRs, and the new test cases for the official KDL test suite likewise. That doc also covers what KdlSharp requires of contributions.

## Repos and branches

Three repos are involved, each on a fork with a branch for this work:

- **KdlSharp:** [sab39/KdlSharp](https://github.com/sab39/KdlSharp), a fork of [AndreyAkinshin/KdlSharp](https://github.com/AndreyAkinshin/KdlSharp), branch `kdlsharp-parsing-fixes`. The branch was started from upstream `main`.
- **KDL spec and test suite:** KdlSharp's `specs` submodule. On the KdlSharp branch, `.gitmodules` points it at [sab39/kdl](https://github.com/sab39/kdl), a fork of [kdl-org/kdl](https://github.com/kdl-org/kdl), where the branch `number-test-cases` adds test cases to the official suite.
- **This module:** branch `kdlsharp-parsing-fixes` of this repo.

On the KdlSharp branch, commits that only change the `specs` submodule (its pointer or its URL) are kept apart from all other changes. They are left out of the KdlSharp PR.

## Setup

The module's branch expects a KdlSharp clone next to it, the same way it already expects `Sab39.Core`:

```
C:\Code\
    KdlSharp\        sab39/KdlSharp, branch kdlsharp-parsing-fixes
    Sab39.Core\
    Sab39.KdlPs\     branch kdlsharp-parsing-fixes
```

Clone KdlSharp with its submodule, so that the official test suite is present:

```
git clone --recurse-submodules --branch kdlsharp-parsing-fixes https://github.com/sab39/KdlSharp
```

## How this module's branch references KdlSharp

- `Sab39.KdlPs.csproj` has a `ProjectReference` to `..\..\KdlSharp\KdlSharp\KdlSharp.csproj` in place of `PackageReference Include="KdlSharp" Version="1.1.0"`. Building the module builds KdlSharp from source, with KdlSharp's own build settings.
- `Sab39.KdlPs.slnx` has a `/KdlSharp/` solution folder with all four projects from KdlSharp's own solution: `KdlSharp`, `KdlSharp.Tests`, `KdlSharp.Benchmarks` and `KdlSharp.Demo`. Having KdlSharp in the solution also makes Visual Studio's fast up-to-date check notice edits to KdlSharp source.
- `KdlSharp.Tests` is xUnit v2 on VSTest, which can't run in Microsoft.Testing.Platform mode, so `dotnet test` can't use MTP for the whole solution. The branch therefore has no `global.json`, which puts `dotnet test` in VSTest mode. MTP refuses to run under VSTest on the .NET 10 SDK, so `Directory.Build.rsp` passes `-p:EnableMSTestRunner=false` to every command-line MSBuild run, and this module's tests run through MSTest's VSTest adapter instead. Visual Studio ignores `.rsp` files and runs both test projects together without this.

Once upstream releases the fixes, the module goes back to a `PackageReference` at the new version, and the branch's reference and solution changes are dropped.

## Next: bringing in the official test suite

Once `Write-KdlNode` and the official test suite tests are in on `main` (see "Testing" in [kdl-powershell-module.md](kdl-powershell-module.md)), `main` is merged into this branch, and the branch's `specs` submodule is repointed to sab39/kdl at the same commit KdlSharp's branch uses. That runs the module's tests against the new test cases too. The branch is then pushed, so others can run them.

## Open: KDL Query Language spec wording

Candidates for wording suggestions to kdl-org/kdl, alongside the test-suite additions. `QUERY-SPEC.md` at kdl-org/kdl commit `89c1087`:

- **`top()`** is defined as "all toplevel children of the current document", and `top() > []` as equivalent to `top()` on its own. Read literally, the definition makes `top() > x` select children of the top-level nodes, which is how KdlSharp implemented it. That contradicts both the stated equivalence and the spec's own example, `top() > package >> name`. The only reading consistent with all three is that `top()` stands for the document root, with an implicit `> []` when it is a whole selector on its own.
- **`||`** is described only as "combines selectors", with the example "selects all `b` and `c` elements". Nothing says whether a node matched by both sides is returned once or twice. KdlSharp returns it twice.

## Open: `KdlReader.ReadValue()`

Out of scope for now, and untested by the branch. The values `ReadValue()` returns don't match what `KdlParser` produces for the same source, and contradict `KdlValue.SourcePosition`'s doc comment ("null if constructed programmatically"):

- No value it returns has a `SourcePosition`. That covers every kind of value, not only `#true`, `#false` and `#null`, which it returns as the shared singletons.
- Strings always have the default `StringType`, whatever their source form.
- Decimal numbers have no `RawText`.
