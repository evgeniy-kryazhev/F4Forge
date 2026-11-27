# Repository Guidelines

## Project Language

Use English throughout the project, including documentation, code identifiers,
comments, log and error messages, commit messages, and pull request descriptions.

## Structure and Architecture

F4Forge targets Windows x64, C++23, .NET 10, and F4SE. `native/src/` integrates
F4SE. `native/lib/commonlibf4/` is an external submodule.

`sdk/<language>/` owns each SDK, including its native adapter and mod-loading policy.
Native adapters compile into `F4Forge.dll`, the single installable file in `build/dist/`.
The managed plugin interface and loader compile into `F4Forge.Net.dll`, which is embedded
in the native DLL and serves as the build-time reference for plugins. Native tests live in `native/tests/`; managed
tests and fixtures live in `tests/dotnet/`. Build artifacts belong in ignored
`build/`, `bin/`, and `obj/` folders.

## Extension Rule — User Approval Required

Add new methods and features through new files, self-registering components, and SDK
extension classes. Do not edit completed code merely to add a feature. If existing code
must change because the feature cannot work otherwise, explain exactly why, identify
the affected files, and obtain explicit user approval before editing them. Do not treat
inconvenience as necessity. An explicit user request to rewrite particular code authorizes
that rewrite. This repository is an MVP; backward version compatibility is not a goal.

Never add per-feature interop exports or central method switches. SDK-specific build
rules belong in that SDK's `xmake.lua`; source discovery must remain automatic.

## Build and Test

Run from the repository root:

```powershell
xmake f -m releasedbg -y
xmake -j 2
xmake run F4Forge.Hosting.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

The hosting test starts the real CLR and loads a fixture plugin. Managed tests use
xUnit and cover discovery, private dependencies, initialization, and failure isolation.
No numeric coverage threshold is imposed.

## Readability and Ownership

Follow `.editorconfig`; use four-space C++/C# indentation, descriptive names, explicit
control flow, and concise comments for invariants. C# types/methods use PascalCase;
private fields use `_camelCase`. Keep warnings as errors. Never let exceptions cross
unmanaged callbacks.

## Contributions and Tools

Use focused imperative commits; existing history also uses `feat:`, `fix:`, and `build:`.
PRs explain behavior, linked issues, tests, and unverified game integration.
Use project skills `cpp-pro` and `dotnet-pinvoke`; consult configured Microsoft Learn
and Context7 MCPs for current C++/.NET documentation. Keep credentials out of the repository.
