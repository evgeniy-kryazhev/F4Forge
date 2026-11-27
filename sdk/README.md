# F4Forge SDK

Reference `src/F4Forge.Net.csproj` from your C# plugin project. Deploy the plugin DLL directly in `Data/F4Forge/` or in a subfolder such as `Data/F4Forge/<plugin>/`. The host searches recursively and supplies the SDK assembly.

```csharp
using F4Forge;

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

Subscribe in `IPlugin.Start` to receive the generated `Events.TESLoadGameEvent` on the thread that raised it:

```csharp
GameEvents.OnGameLoaded += (_, _) => context.Log.Info("Game loaded.");
GameEvents.OnBeforeNewOrLoadCompleted += (_, _) => context.Log.Debug("New game or load is about to complete.");
GameEvents.OnCellFullyLoaded += (_, args) => context.Log.Debug($"Loaded cell at 0x{args.Cell:X}.");
GameEvents.OnObjectLoadChanged += (_, args) =>
    context.Log.Debug($"Object {args.FormID:X8}: loaded={args.Loaded}");
```

`OnDeath` delivers the deceased actor on the thread that raised the event. Use `ActorEvents.OnDeath -= LogDeath` to unsubscribe. A plugin that also needs the killer or the dying phase can subscribe to `Events.TESDeathEvent` through `Bridge.On`. Generated methods are available through `Bindings`, for example `Bindings.Actor.IsInCombat(victim.Handle)`.
