# Console commands

Custom aliases entered directly in the Fallout 4 console are routed through the
existing message channel and `IDataSerializer`. The native module registers
itself; `context.GetSingleton<GameConsole>()` provides the mod's scoped command service.

## Mod API

```csharp
using F4Forge.Net.Console;

context.GetSingleton<GameConsole>().AddHandler("hello", command =>
{
    var name = command.Arguments.Count > 0 ? command.Arguments[0] : "Wasteland";
    command.WriteLine($"Hello, {name}!");
});
```

Input: `hello "World"`. Aliases are case insensitive and match
`[A-Za-z_][A-Za-z0-9_.-]*`, up to 64 characters. Duplicate aliases are rejected.
The native adapter reserves built-in console and script commands, including
their short names. The router forwards reserved and unknown commands unchanged.

Double quotes group arguments; `""` is an empty argument. Within quotes, `\"`
and `\\` escape a quote and backslash. Other backslashes are preserved. Single
quotes have no special meaning. Arguments may contain Unicode.

Handlers are synchronous. `WriteLine()` buffers literal output until successful
return; output is discarded if the handler throws. Do not use `async void` or
retain the context for later writes. Dispose the returned token to unregister
early. Registrations made through the mod's scoped channel are removed on mod
shutdown and startup rollback.

## Shared channel contract

- `console.validate_alias`: `{alias}` → `true`, or a normal channel error.
- `console.command.<lowercase-alias>`: native-to-SDK callback with `alias`,
  `arguments`, `rawCommand`, `rawArguments`, `selectedReferenceFormId`, and
  `parseError`; returns `{lines: string[]}`.

The selected reference is a form ID (`uint`, zero means absent). No pointers
cross the channel. Requests, results and errors use the channel's
`IDataSerializer`; there are no console-specific interop exports. A parse error
is sent only to an existing handler; unknown input remains available to the game.
The .NET service rejects parse errors before calling mod code.

`context.GetSingleton<GameInterception>().OnConsoleCommandExecuting` runs before routing, including
for built-in commands. It can replace `Command` or set `Skip` to cancel the entire
action. Interceptors execute synchronously in registration order; a failed or
invalid decision preserves earlier changes. The original input is retained in
`OriginalCommand`. See [async lifecycle](../../../docs/async-lifecycle.md) for
notification overloads, cancellation, and game-thread scheduling.

## Engine integration and lifetime

`module.cpp` resolves CommonLib's `Console::ExecuteCommand` function through
Address Library and installs MinHook after game data is ready. It copies the
selected reference's form ID; SDK callbacks receive no native objects. Hook
failures are logged through the existing game adapter error boundary.

Dispatch uses the game runtime's callback lease. Stopping the host disables new
routing and waits for active callbacks before releasing the router. The entry
hook and original trampoline remain for the process lifetime as a pass-through;
this avoids freeing a trampoline that another thread has already entered. Data
unreadiness also disables routing. Repeated readiness messages and host restart
do not install duplicate hooks. Unloading `F4Forge.Core.dll` itself is unsupported.

MinHook is linked statically into Core, so no extra runtime DLL is needed. Its
upstream license notices are stored in `LICENSE.MinHook.txt` and embedded in
Core as the `F4FORGE_MINHOOK_LICENSE` RCDATA resource.

## Verification

```powershell
xmake f -P native/tests/console -m releasedbg -y
xmake -P native/tests/console -j 2
xmake run -P native/tests/console F4Forge.Console.Tests
xmake run -P native/tests/console F4Forge.Console.Hook.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Native tests cover routing with JSON and binary serialization, parsing, reserved
aliases, errors, disposal and restart. Managed tests cover registration,
deserialization, callback lifetime, disposal and mod scope cleanup. The hook
suite exercises actual MinHook detours on a test function, original forwarding,
restart, readiness changes and draining active callbacks on stop.

**Game integration has not been verified by launching Fallout 4.** Compilation
and automated tests do not verify the game's actual call path or text rendering.
Game testing must verify direct aliases, selected references, normal vanilla
commands, menu reopening and unloading a mod while the console is open.
