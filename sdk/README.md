# F4Forge SDK

Reference `src/F4Forge.Net.csproj` from your C# plugin project. Deploy the plugin DLL directly in `Data/F4Forge/` or in a subfolder such as `Data/F4Forge/<plugin>/`. The host searches recursively and supplies the SDK assembly.

```csharp
using F4Forge.Actors;
using F4Forge.Plugins;

public sealed class DeathLogPlugin : IPlugin
{
    public void Start(PluginContext context)
    {
        context.Log.Info("DeathLog started.");
        void LogDeath(Actor victim)
        {
            if (Player.GetSingleton() is { } player && victim == player)
            {
                context.Log.Info($"Player {victim.FormId:X8} died.");
            }
        }
        ActorEvents.OnDeath += LogDeath;
    }
}
```

`context.Log.Debug`, `Info`, `Warn`, and `Error` append UTF-8 messages to `Data/F4SE/Plugins/F4Forge/Net.log`. Each plugin entry includes a UTC timestamp, severity and assembly name. Use `context.Log.Error("Loading settings failed.", exception)` to include exception details.

`Player.GetSingleton()` reads the current player and returns null when unavailable. `Actor.Handle` exposes the borrowed native address for calls through `Bridge.Invoke`. Use actor references only while their game objects are alive.

Import `F4Forge.Game` and subscribe in `IPlugin.Start` to receive game events on the thread that raised them.

```csharp
GameEvents.OnGameLoaded += (_, _) => context.Log.Info("Game loaded.");
GameEvents.OnBeforeNewOrLoadCompleted += (_, _) => context.Log.Debug("New game or load is about to complete.");
GameEvents.OnCellFullyLoaded += (_, args) => context.Log.Debug($"Loaded cell at 0x{args.Cell:X}.");
GameEvents.OnObjectLoadChanged += (_, args) =>
    context.Log.Debug($"Object {args.FormID:X8}: loaded={args.Loaded}");
```

`OnDeath` delivers the deceased actor on the thread that raised the event. Use `ActorEvents.OnDeath -= LogDeath` to unsubscribe. Import `F4Forge.Bridge` for `Bridge`, `Bindings`, and `Events`. A plugin that also needs the killer or the dying phase can subscribe to `Events.TESDeathEvent` through `Bridge.On`. Generated methods are available through `Bindings`, for example `Bindings.Actor.IsInCombat(victim.Handle)`.

## Keyboard and mouse input

Import `F4Forge.Input` and subscribe in `IPlugin.Start` to observe the input that Fallout 4 delivers to `MenuControls`. Button codes are specific to the device. The example logs the first press of a keyboard key (`Device == 0`) or mouse button (`Device == 1`).

```csharp
InputEvents.OnButton += (_, button) =>
{
    if (button.Device is 0 or 1 && button.Value != 0 && button.HeldDownSecs == 0)
    {
        context.Log.Info($"Device {button.Device} pressed button {button.IdCode}.");
    }
};
```

`OnCharacter` supplies `CharCode`, `OnMouseMove` supplies relative `MouseInputX` and `MouseInputY`, and `OnCursorMove` supplies `CursorPosX` and `CursorPosY`. All four handlers receive copied values synchronously with a null sender. Plugins can also subscribe directly, for example with `Bridge.On<Events.ButtonEvent>(Events.ButtonEvent.Name, handler)`.
