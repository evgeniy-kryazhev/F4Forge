# F4Forge

A minimal modular SDK host for Fallout 4 / F4SE on Windows x64.

- `F4Forge.Core.dll` — native core, message channel, F4SE integration, and native SDK loaders.
- `F4Forge.Net.dll` — C# API, .NET mod loading, and the managed side of the channel.
- `sdk/<language>/` — an independent SDK that defines its own mod-loading rules.

Game features are not available yet. Backward compatibility with older API versions is not supported.

## Build and Test

Requires MSVC with C++23 support, .NET SDK 10 x64, xmake 3.1+, and the submodule dependencies:

```powershell
git submodule update --init --recursive
xmake f -m releasedbg -y
xmake -j 2
xmake run F4Forge.Channel.Tests
xmake run F4Forge.Events.Tests
xmake run F4Forge.Components.Tests
xmake run F4Forge.Hosting.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

The two DLLs required for installation are in `build/bin/`. PDB, LIB, and EXP files are only needed for development.
Place `F4Forge.Core.dll` in `Data/F4SE/Plugins/` and `F4Forge.Net.dll` in
`Data/F4SE/Plugins/F4Forge/`. Only native plugins should be placed directly in
`Plugins`: the managed SDK DLL interrupts F4SE's plugin scan. When upgrading, remove the old
`Data/F4SE/Plugins/F4Forge.Net.dll`; in MO2, install the archive by fully replacing the mod.
Players need F4SE and .NET Runtime 10 x64. No separate `nethost.dll` is required:
its code is included in Core. The CLR startup configuration is created in a temporary file and then deleted.

## .NET Mods

Place mod DLLs in `Data/F4ForgeNet/`; subfolders of any depth are supported.
The loader searches for DLLs recursively without manifests. Libraries that do not implement `IMod`,
SDK copies, and native DLLs are skipped. Each mod DLL references `F4Forge.Net.dll`
and contains exactly one public class implementing `IMod` with a parameterless constructor:

```text
Data/
  F4SE/Plugins/
    F4Forge.Core.dll
    F4Forge/
      F4Forge.Net.dll
  F4ForgeNet/
    MyMod.dll
    OtherMod/
      OtherMod.dll
      Dependency.dll
```

Place dependencies next to the mod DLL. `context.Directory` points to that DLL's folder.
Mods are discovered in path order. Each mod's `StartAsync` runs independently;
completion order does not provide a dependency mechanism between mods.

```csharp
public sealed class MyMod : IMod
{
    public Task StartAsync(ModContext context, CancellationToken cancellationToken)
    {
        context.Messages.Register<object?, string>("mymod.title", _ => "My mod");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // Stop background work and release resources owned by the mod.
        return ValueTask.CompletedTask;
    }
}
```

The SDK automatically removes registrations when a mod stops or fails to start.
Shutdown cancels startup and asynchronous notifications, waits for them to complete,
calls `DisposeAsync`, and unloads the assembly context. The game thread does not wait for this process.
Notifications, interceptors, and returning to the game thread are described in
[async-lifecycle.md](docs/async-lifecycle.md).
Typed notifications use `context.Events.Subscribe<TEvent>(handler)`;
the channel name is defined once in the event type through `IGameEvent.EventName`.
Data requests use `Messages.Query(IQuery<TResult>)`, and state changes use
`Messages.Execute(ICommand<TResult>)`; contracts and examples are described in
[operations.md](docs/operations.md).
Dynamic calls to functions and properties in other Papyrus scripts are available through
`context.GetSingleton<GamePapyrus>()`; PrismaUI and F4SE UI examples are in [papyrus.md](docs/papyrus.md).
Mods are isolated through `AssemblyLoadContext`; individual mod errors are logged to
`F4Forge/Net.log`. Automatic mod reloading is not implemented yet.

## Shared Channel and Extensions

The SDK uses the existing channels and typed contracts:

| Purpose | API | Namespace |
| --- | --- | --- |
| Calls and responses | `context.Messages`, `IMessageChannel` | `F4Forge.Net.Messaging` |
| Typed queries and state changes | `Query`, `Execute`, `IQuery<T>`, `ICommand<T>` | `F4Forge.Net.Operations` |
| Notifications | `context.Events`, `IEventChannel`, `IGameEvent` | `F4Forge.Net.Events` |
| Game object resolution and types | `context.GetSingleton<GameObjects>()`, `Form`, `IFormQuery<T>`, `IFormType<T>` | `F4Forge.Net.Objects` |
| Console handlers | `context.GetSingleton<GameConsole>()`, `IConsole` | `F4Forge.Net.Console` |
| Synchronous decisions before an action | `context.GetSingleton<GameInterception>()` | `F4Forge.Net.Interception` |
| Script calls | `context.GetSingleton<GamePapyrus>()` | `F4Forge.Net.Papyrus` |
| Mod diagnostics | `context.GetSingleton<GameDiagnostics>()` | `F4Forge.Net.Diagnostics` |
| Game thread scheduling | `context.GetSingleton<GameThread>().RunAsync(...)` | `F4Forge.Net.Singletons` |

`Query` and `Execute` call the same `IMessageChannel`; they have no separate transport.
`GetSingleton<T>()` is available through `F4Forge.Net.Singletons.SingletonExtensions`.
Import it with `using static F4Forge.Net.Singletons.SingletonExtensions;` when importing
all generated singleton types would cause a name collision with domain types.
Facade names follow the [SDK naming standard](docs/naming.md).
`GetSingleton<GameObjects>().Query` also creates an object of the required C# type from the channel response.
`IFormType<T>` defines type checking and wrapper creation with the same `FormId`, `Session`,
and mod channel. Custom types can be added without a central registry;
see [foundations.md](docs/foundations.md) for an example.

Specific events, queries, and commands are organized into modules by purpose:
`Player`, `Actors`, `Objects`, `Game`, `Saves`, and `UI`.
Console handlers are grouped under `Console`; state-changing commands are placed
in the `Commands` subfolders of their respective modules.

Request: `{"method":"mymod.title","arguments":null}`.
Response: `{"success":true,"value":"My mod"}` or
`{"success":false,"error":{"code":"method_not_found","message":"..."}}`.

C#: `context.Messages.Call<string>("mymod.title", null)`.
C++: register a handler through `Channel::Add` and call it through `Channel::Call`.
The channel is synchronous, serializes handler execution, and allows nested calls on the same thread.
Do not wait inside a handler for another thread that accesses the channel. JSON
buffers are copied during the call; pointers and game objects are not passed through the channel.
The message size limit is 1 MiB. The channel does not automatically dispatch calls to the game thread.

Native modules start before components in the `ComponentPhase::Sdk` phase.
To add a feature, add a file in `native/modules/` with `ComponentRegistration` and a separate
C# extension class in the SDK. The central router and interop table remain unchanged.
To add an SDK, add `sdk/<language>/xmake.lua` with the `f4forge.sdk.<language>` rule and
`native/*.cpp` files with component registration. Run `xmake f` again after adding files.
All native adapters are included in Core; new language DLLs use their own `F4Forge.<Language>` name.

## Development Tools

Skills: `.agents/skills/cpp-pro` from `Jeffallan/claude-skills` and
`.agents/skills/dotnet-pinvoke` from the official `dotnet/skills` repository.
Project MCPs in `.codex/config.toml`: Microsoft Learn (MSVC/C++/.NET) and Context7
(library documentation). After installation, restart the MCP connections; skills become available
on the next turn. MCPs are only needed by the development assistant, not by the build or the game.

## Events

The event channel is available through `channel.Events()` in C++ and `context.Events` in C#:

```cpp
channel.Events().Publish("game.loaded", {{"saveName", saveName}});
```

```csharp
var subscription = context.Events.Subscribe<GameLoaded>(
    "game.loaded",
    notification => Console.WriteLine(notification.SaveName));

context.Events.Publish("mymod.ready", new { name = "My mod" });
subscription.Dispose();

public sealed record GameLoaded(string SaveName);
```

This example shows publication from a game module: the channel itself does not install game hooks.
Subscriptions belong to the mod and are removed on shutdown or startup failure. Subscribers
are called sequentially, in registration order, on the publishing thread. There is no history or
redelivery. Subscriber errors are logged without interrupting other subscribers.
New subscriptions do not receive a publication that has already started; subscriptions removed before their turn are skipped.

Messages and events share a serial execution lane that allows
nested calls on the same thread. The registry lock is not held while handlers run.
Unsubscribing prevents further calls and waits for an active handler; unsubscribing from within
the handler itself returns immediately. The handler then finishes its current call.
The host shuts down the entire SDK outside handlers; reloading from a handler is not supported.

## Shared Serialization

`IDataSerializer` in C++ and C# is used for requests, arguments, results, errors,
and events. Both channels on each side use the same serializer instance.
In C#, it is also available through `context.Serializer`; method handlers are typed:

```csharp
context.Messages.Register<RenameRequest, bool>("mymod.rename", request =>
{
    return Rename(request.Name);
});
```

The transport passes bytes with an explicit length, including null bytes. UTF-8 JSON is the current
default implementation, not a transport requirement. JSON conversion is contained in `json_serializer.cpp`
and `JsonDataSerializer.cs`. Native handlers use `DataValue`, a value tree
based on nlohmann; alternative encodings must support the same values.
Method and event names and the format identifier remain UTF-8 strings.

A new format requires two `IDataSerializer` implementations: C++ with `SerializerRegistration`
and C# with a public parameterless constructor in the SDK assembly. They must encode
the protocol fields `method`, `arguments`, `event`, `data`, `success`, `value`, `error`, `code`,
and `message` identically. Select the format through the `F4FORGE_DATA_FORMAT` environment variable before starting the game
(default: `json`). The SDK automatically discovers the matching implementation;
unknown or mismatched formats are rejected before mods are loaded.

Serializers must be thread-safe, must not retain input buffers, and must signal
parsing errors by throwing an exception. The format is fixed when the host is created.
