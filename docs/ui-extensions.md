# Reusable UI and Papyrus extensions

These capabilities belong to F4Forge and contain no MCM-specific script names, setting formats, or UI assets.
Engine operations use the existing scheduler, serializer, channels, and mod lifetime.
No new interop exports or central dispatch table entries are required.

## PrismaUI_F4 views

```csharp
using F4Forge.Net.UI;

var ui = context.GetSingleton<GamePrismaUi>();
if (!await ui.IsAvailableAsync(cancellationToken))
{
    throw new InvalidOperationException("PrismaUI_F4 API v2 is required.");
}

var view = await ui.CreateAsync("MyMod/index.html", message =>
{
    context.Events.Publish("mymod.ui.message", message);
}, cancellationToken);
await view.WaitUntilReadyAsync(cancellationToken);
await view.SendAsync("application-defined text");
await view.ShowAsync();
```

JavaScript sends text with `window.f4forge(text)` and receives text in
`window.f4forgeReceive = function (text) { ... }`. Application serialization is chosen by the mod.
Renderer callbacks copy borrowed data and queue publication on the game thread.
Readiness is subscribed before native creation, so immediate completion is supported.
Dispose detaches callbacks synchronously and schedules unfocus/destruction without retaining managed callback state.
Destruction runs directly on the F4SE task queue, even after the event runtime stops;
it retains only the process-lifetime renderer API and the view handle.

The Prisma callback ABI has no userdata. This adapter reserves non-reused callback slots to prevent stale callbacks
from addressing new owners. The process supports up to 256 view creations; repeated show/hide does not consume slots.
Core must remain loaded while Prisma callbacks can execute.

The copied upstream header explicitly permits use in mods. It comes from
`NomadsReach/framework-F4-Conversion` commit `d71e84f9b872cd30aebe848b1ef266173157db9c`.
F4Forge does not redistribute the renderer or modify PrismaUI_F4.

## Native Papyrus callbacks and external events

`GamePapyrusFunctions.RegisterAsync` binds a static native function declared in a separately compiled Papyrus script.
Signatures support `none`, `bool`, `int`, `float`, and `string`; parameters cannot be `none`.
Handlers are synchronous and must never wait on asynchronous work that needs the shared channel or game thread.

```csharp
using F4Forge.Net.Papyrus;

var functions = context.GetSingleton<GamePapyrusFunctions>();
var registration = await functions.RegisterAsync(
    "MyNativeApi", "Count", "int", ["int"],
    arguments => arguments[0].GetInt32() + 1,
    cancellationToken);

await context.GetSingleton<GamePapyrusEvents>().SendExternalAsync(
    "MyModChanged", ["SettingName", 42], cancellationToken);
```

Papyrus event receivers can forward their arguments to such registered native functions, giving C# an owned callback.
Receiver scripts and their `RegisterForExternalEvent` / unregistration policy belong to the consuming mod;
the SDK does not automatically attach scripts or invent a receiver quest.
`SendExternalAsync` enumerates F4SE registrations and dispatches their callbacks with copied primitive arguments.

The VM owns registered function objects permanently. Disposing a registration revokes its managed callback;
the remaining VM stub safely fails subsequent calls. Registration names cannot currently be reused in the same process.
Shutdown disables bindings before the channel is released, including partial startup rollback.

## Pause menu and Scaleform

`GamePauseMenu.Register(label, Func<Task>)` adds an independent entry to the standard pause menu.
Multiple mods can register entries. Selection callbacks use the mod's tracked asynchronous event lifetime.
Disposal removes the entry and its callback; no replacement `MainMenu.swf` is distributed.
The native pause-menu movie-advance hook reconciles owned entries after the engine updates the list,
including later `InitList` rebuilds. It preserves other entries and inserts registered items near the top.
The dynamic timeline root retains the selection callback; the sealed `MainMenu` instance is not extended.
Startup, registration, menu opening, and list synchronization are logged as `Pause extensions`.

The standalone regression executable checks delayed population, full list replacement, multiple consumers,
duplicate repair, Unicode labels, and final cleanup without loading the game. From an x64 Visual Studio
Developer Command Prompt at the repository root:

```bat
cl /nologo /std:c++latest /EHsc /W4 /WX /utf-8 /Fe:build\tests\F4Forge.PauseMenu.Tests.exe /Fo:build\tests\pause_menu_tests.obj native\tests\pause_menu_tests.cpp
build\tests\F4Forge.PauseMenu.Tests.exe
```
The adapter relies on the standard Fallout 4 menu list path and must be verified against custom menu replacements.

`GameScaleform` provides copied primitive/array reads, writes, and function calls on open movies.
The non-generic `InvokeAsync` overload discards arbitrary return values, which is useful for external plugin callbacks.
No Scaleform objects or pointers cross the SDK boundary.

## Supporting game data

- `GameGlobals`: read/write TESGlobal values by plugin-local ID, including light plugins; an empty plugin uses a runtime ID.
- `GamePluginFiles`: check whether an ESP/ESM/ESL is active.
- `GameNativePlugins`: inspect loaded native DLL dependencies without loading them.
- `GameFormText`: localized form descriptions.
- `GameResources`: text reads through engine resources, including BA2 archives, with a 128 KiB limit.
- `GameInput`: copied pause/modifier state and XInput controller state without consuming engine input.
- `ExecuteConsoleCommandAsync`: engine console execution preserving the existing command hooks.

Managed transport tests cover scope isolation, late messages, immediate readiness, nested calls, failed creation,
owned cleanup, and an alternate serializer. Real VM, focus, resource archives, controller routing, and pause-menu
integration still require game acceptance on the target runtime.
