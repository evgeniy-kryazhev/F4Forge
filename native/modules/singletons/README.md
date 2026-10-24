# Generated singleton API

The .NET SDK exposes CommonLibF4 singletons through one constrained generic extension:

```csharp
using F4Forge.Net.Singletons;
using GameUI = F4Forge.Net.Singletons.UI;

var controls = context.GetSingleton<PlayerControls>();
var camera = context.GetSingleton<PlayerCamera>();
var ui = context.GetSingleton<GameUI>();
var gameThread = context.GetSingleton<GameThread>();

bool pauseMenuOpen = await gameThread.RunAsync(() =>
{
    if (!ui.IsAvailable)
    {
        return false;
    }
    return ui.GetMenuOpen("PauseMenu");
});
```

`GetSingleton<T>()` creates no engine object and performs no engine access. It returns
the same typed proxy for the same mod context and type. Different mod contexts receive
different proxies using their own channels. The generic constraint rejects unrelated
types at compile time. Generated factories use static interface dispatch, without
reflection, a central type switch, or a global registry of mod instances.

`GameThread` lazily creates the scheduler for its mod context. It uses the context's
shared unload lifetime and preserves task cancellation and exception handling.
Both `RunAsync` overloads can be called from any thread. Obtaining the singleton
does not register native handlers or enqueue work.

The remaining mod services use the same generic entry point. Their public types
remain in their feature namespaces:

| Service type | Namespace | Purpose |
| --- | --- | --- |
| `GameConsole` | `F4Forge.Net.Console` | Console aliases and handlers |
| `GameDiagnostics` | `F4Forge.Net.Diagnostics` | Mod identity, operation history, and hook status |
| `GameObjects` | `F4Forge.Net.Objects` | Typed form queries |
| `GameInterception` | `F4Forge.Net.Interception` | Synchronous native decisions |
| `GamePapyrus` | `F4Forge.Net.Papyrus` | Dynamic VM calls and script properties |

```csharp
using F4Forge.Net.Console;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Interception;
using F4Forge.Net.Objects;
using F4Forge.Net.Papyrus;
using static F4Forge.Net.Singletons.SingletonExtensions;

var console = context.GetSingleton<GameConsole>();
var diagnostics = context.GetSingleton<GameDiagnostics>();
var objects = context.GetSingleton<GameObjects>();
var interceptors = context.GetSingleton<GameInterception>();
var papyrus = context.GetSingleton<GamePapyrus>();
```

Services are constructed once per type and mod context, including concurrent first
lookups. Registrations and pending work retain the mod's existing lifetime; native
cleanup uses the original transport during shutdown. Unused services are not created.
`ModContext` keeps `Directory`, `Messages`, `Events`, and `Serializer` as context data
and transport infrastructure. Service properties are no longer exposed separately.

Importing `SingletonExtensions` with `using static` enables extension-method lookup
without importing all generated engine types, avoiding names shared with managed
object wrappers such as `ActorValue`.

Each engine proxy member invocation resolves `RE::Type::GetSingleton()` again. The game retains
ownership; proxies contain no native pointers and must not dispose engine objects.
`IsAvailable` returns `false` for a null native singleton; other members fail through
the channel's standard error envelope. Availability can change between calls.

Engine proxy members, including `IsAvailable`, require the game thread. Use synchronous lambdas
inside `context.GetSingleton<GameThread>().RunAsync`. This check is a baseline: engine methods can
have additional state or subsystem requirements that declarations do not describe.
The generator does not infer semantic safety from C++ types.

## Generation and build

`sdk/dotnet/tools/SingletonGenerator` parses the actual CommonLibF4 C++ AST with
ClangSharp/libClang 21.1.8. The native parsing libraries are restored from pinned NuGet
dependencies. No separately installed Clang executable is required.

Prerequisites are .NET 10, Visual Studio C++ x64 tools, Windows SDK, initialized
CommonLibF4 submodules, and the xmake dependency cache populated by configuration.
The parser locates Visual Studio using `vswhere` and discovers MSVC/Windows SDK
include directories. `F4FORGE_SPDLOG_INCLUDE` can override the spdlog include directory;
otherwise the normal xmake user cache is searched.

The new SDK-local `Directory.Build.targets` runs generation before compiling
`F4Forge.Net`. Header and generator changes invalidate the generation stamp. Missing
generated files also trigger generation. The existing xmake SDK dependency builds
`F4Forge.Net` before the native core; native source discovery remains unchanged.

Generated output is checked in for review:

- `sdk/dotnet/F4Forge.Net/Singletons/Generated/Singletons.g.cs`: typed proxies and enums.
- `native/modules/singletons/generated.inc`: channel registrations compiled by the module.
- `sdk/dotnet/tools/SingletonGenerator/bindings-report.json`: discovered types, routes,
  and reasons for skipped declarations.

To regenerate explicitly from the repository root:

```powershell
dotnet run --project sdk/dotnet/tools/SingletonGenerator -c Release -- .
```

Generation is deterministic and does not rewrite unchanged output. Parser errors fail
the build instead of replacing output with bindings from an incomplete AST. Keep the
managed/native outputs together when building or distributing the SDK.

## Supported declarations

Discovery includes public static parameterless `GetSingleton()` functions returning
their enclosing non-template class by pointer or reference. Namespace and nested-class
names are flattened for managed types, for example `BSGraphics_State`.

Generated members are directly declared public inline, non-static, non-virtual
methods and read-only properties for public scalar fields, including bitfields.
Supported wire types are booleans, fixed-width integers, floating-point values,
enums with known definitions, and `RE::BSFixedString` copied as a managed string.
Const reference inputs for these types are copied. Void methods return normally.
Native parameter names are retained, removing the CommonLibF4 `a_` prefix.

Overloads receive deterministic signature-specific channel routes. Numeric input
types and ranges are checked before conversion. Embedded NUL characters in native
strings are rejected on both sides. The existing `IDataSerializer` owns all wire
encoding, including errors; generated bindings do not parse JSON text.

Pointers, mutable references, arbitrary structures/containers, callbacks, templates,
virtual methods, inherited members, and out-of-line methods are not projected in this
version. Unsupported direct declarations appear in the report. Native fields are
read-only in C#: mutation requires a supported native method. C++ default argument
expressions are not translated; pass every generated argument explicitly.

New matching classes and members require no handwritten proxy or native registration.
Supporting another wire type requires extending the generator's shared conversion
rules and testing its ownership and serialization contract.

## Validation

```powershell
dotnet run --project sdk/dotnet/tools/SingletonGenerator -c Release -- . --self-test
Push-Location native/tests/singletons
xmake f -P . -m releasedbg -y
xmake -P . -j 2
xmake run -P . F4Forge.Singletons.Tests
Pop-Location
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

The isolated native target compiles generated bindings against a fake engine, testing
null/replaced/reference singletons, methods and fields, overloads, enums, Unicode,
thread checks, malformed values, registration rollback/restart, and alternate wire
serialization. The generator self-test checks AST filtering and deterministic output.
Managed tests exercise real generated proxies through the native test transport.
Real Fallout 4 integration still requires in-game verification.

The isolated native tests also pass with AddressSanitizer. To enable it, configure
from the test directory with `xmake f -P . -m releasedbg --policies=build.sanitizer.address -y`
and rebuild. Keep the test working directory separate to avoid sharing the main
project's xmake configuration. MSVC does not provide UndefinedBehaviorSanitizer.
