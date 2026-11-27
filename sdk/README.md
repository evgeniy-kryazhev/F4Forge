# F4Forge SDK

Reference `src/F4Forge.Net.csproj` from your C# plugin project and deploy the plugin DLL in `Data/F4ForgeNet/<plugin>/`. The host supplies the SDK assembly.

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

`OnDeath` delivers the deceased actor on the thread that raised the event. Use `ActorEvents.OnDeath -= LogDeath` to unsubscribe. A plugin that also needs the killer or the dying phase can subscribe to `Events.TESDeathEvent` through `Bridge.On`. Generated methods are available through `Bindings`, for example `Bindings.Actor.IsInCombat(victim.Handle)`.
