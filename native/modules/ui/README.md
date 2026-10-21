# UI queries

`ui.is_pause_menu_open` takes no arguments and returns a boolean through the shared
message channel and serializer. It reads `PauseMenu` directly from the engine menu
stack on each call and returns `false` when the UI singleton is unavailable.

In the .NET SDK, import `F4Forge.Net.UI` and call on the game thread:

```csharp
bool isOpen = context.IsPauseMenuOpen();
```

From another thread, use the existing game-thread scheduler:

```csharp
bool isOpen = await context.GameThread.RunAsync(() => context.IsPauseMenuOpen());
```

Direct calls from other threads fail through the channel's standard error envelope.
The typed query is also available as `F4Forge.Net.UI.Queries.IsPauseMenuOpen`:

```csharp
bool isOpen = context.Messages.Query(new IsPauseMenuOpen());
```

The typed-query example additionally requires `F4Forge.Net.Operations` and
`F4Forge.Net.UI.Queries`. The component unregisters its method when the host stops.

Run the isolated native tests from the repository root:

```powershell
xmake f -P native/tests/ui -m releasedbg -y
xmake -P native/tests/ui -j 2
xmake run -P native/tests/ui F4Forge.UI.Tests
```

In-game verification: query during gameplay, after opening the pause menu with
Escape, and after closing it. Expected results: `false`, `true`, `false`.
