# Game events

The `game_events` native component connects F4SE/CommonLib sources to the existing
event channel. The bridge and adapter registry live here; event implementations
belong to their game subsystems. All payloads use the channel's `IDataSerializer`.

| Subsystem | Native event files | SDK namespace and directory |
| --- | --- | --- |
| Game lifecycle | `native/modules/game/events/` | `F4Forge.Net.Game.Events` / `Game/Events/` |
| Saved games | `native/modules/saves/events/` | `F4Forge.Net.Saves.Events` / `Saves/Events/` |
| Actors | `native/modules/actors/events/` | `F4Forge.Net.Actors.Events` / `Actors/Events/` |
| Player movement | `native/modules/player/events/` | `F4Forge.Net.Player.Events` / `Player/Events/` |
| Menus and UI | `native/modules/ui/events/` | `F4Forge.Net.UI.Events` / `UI/Events/` |

The SDK paths above are relative to `sdk/dotnet/F4Forge.Net/`. Each notification
implements `IGameEvent` and declares its stable `EventName`. All subsystems use
the same three `context.Subscribe<TEvent>(handler)` overloads.

```csharp
using F4Forge.Net.Actors.Events;
using F4Forge.Net.Events;
using F4Forge.Net.Player.Events;
using F4Forge.Net.Saves.Events;
using F4Forge.Net.Logging;

// Inside IMod.StartAsync(ModContext context, CancellationToken cancellationToken):
context.Subscribe<SaveLoadedEvent>(notification =>
{
    context.LogInfo($"Save loaded: {notification.Success}");
});

context.Subscribe<ActorDiedEvent>(notification =>
{
    if (notification.IsPlayer)
    {
        context.LogInfo("The player died");
    }
});

context.Subscribe<PlayerLocationChangedEvent>(notification =>
{
    context.LogInfo($"Location: {notification.NewLocationFormId:X8}");
});
```

All subscription overloads return `IDisposable`. Subscriptions made through `context.Events`
are removed when the mod stops. Disposing a subscription early is also supported.

## Available notifications

| Channel name | SDK record | Payload |
| --- | --- | --- |
| `game.runtime.loaded` | `RuntimeLoadedEvent` | Empty; F4SE's one-time startup notification |
| `game.data.ready` | `GameDataReadyEvent` | `Ready`: true or false, as reported by F4SE |
| `game.new` | `NewGameEvent` | Empty; new game created, world may still be loading |
| `game.save.loading` | `SaveLoadingEvent` | `SaveName` |
| `game.save.loaded` | `SaveLoadedEvent` | `SaveName`, `Success` (including failed loads) |
| `game.save.saving` | `SaveSavingEvent` | `SaveName` |
| `game.save.saved` | `SaveSavedEvent` | `SaveName`; save routine returned, success is not guaranteed |
| `game.actor.died` | `ActorDiedEvent` | `ActorFormId`, `KillerFormId`, `IsPlayer` |
| `game.player.cell.changed` | `PlayerCellChangedEvent` | `PreviousCellFormId`, `NewCellFormId` |
| `game.player.location.changed` | `PlayerLocationChangedEvent` | `PreviousLocationFormId`, `NewLocationFormId` |
| `game.menu.changed` | `MenuChangedEvent` | `MenuName`, `Opening` |

Wire properties use camelCase. IDs are unsigned 32-bit Form IDs; zero means absent or
unknown. `SaveName` is nullable and preserves F4SE's name, rather than constructing
a filesystem path. For load completion it is copied from the corresponding start
notification; an unmatched completion has a null name.

Death includes NPCs and the player, only when `TESDeathEvent.dead` is true. A missing
dying actor is ignored; a missing killer is zero. Cell events describe entry, suppress
duplicate IDs, and reset the previous ID to zero at a load/new-game boundary. The
first entry after that reset is reported with an unknown previous cell.

Cell boundaries and named `BGSLocation` boundaries are different. A named location
can change without the player changing cell, and either old/new location can be null.
The location adapter intercepts `TESActorLocationChangeEvent`, filters for the player,
copies IDs and calls the original handler before publishing. It does not poll.

## Engine integration and extension

The F4SE listener is installed during component startup. Engine sources attach when
game data becomes ready; messages arriving later retry attachment if a singleton was
not yet available. Sources are detached when readiness becomes false and on shutdown.
F4SE exposes no listener removal, so its permanent callback becomes inactive after
shutdown. Shutdown waits for active adapter leases before releasing the channel.
Host shutdown must run outside event handlers, as for the rest of the SDK.

Callbacks start in the engine publisher's thread, which may differ between sources.
Async callbacks are tracked without making the publisher wait for their continuations.
No history is replayed and no automatic game-thread scheduling is performed.
Keep handlers short and never wait for another thread that needs the channel. A mod
subscriber's exception is logged without preventing delivery to other subscribers.

Location interception uses `RE::VTABLE::GameScript__BasicEventHandler[16]`, resolved by
`REL::ID` through Address Library, and the `ProcessEvent` virtual slot. Its original
return value is preserved. Detachment restores the slot only if it still belongs to
this module, preserving hooks installed later by other plugins. The Core DLL remains
loaded while chained wrappers exist. The missing event payload declaration is local
to this module and has size/offset assertions; CommonLib is not modified.

References: [F4SE messaging contract](https://github.com/ianpatt/f4se/blob/master/f4se/PluginAPI.h),
[actual save/load payloads](https://github.com/ianpatt/f4se/blob/master/f4se/Hooks_SaveLoad.cpp),
[location event layout](https://github.com/Neanka/f4se/blob/master/f4se/f4seee/f4seeeEvents.h).
Address Library supplies addresses; event layouts and virtual slots still follow the
CommonLib version used to build Core. This change does not extend supported runtimes.

To add another event adapter, add a file under its subsystem's `events/` directory and an
`AdapterRegistration` with a unique name, message callback and cleanup callback.
An adapter can attach a new engine sink after `Runtime::DataReady()` becomes true.
Use `Runtime::Invoke` at native callback boundaries and publish copied values through
`Runtime::Publish`. Never retain borrowed engine event pointers. Lifecycle publication
has order 100 so the other adapters attach before mods receive readiness notifications.
Save correlation resets run at order 50, before general lifecycle notifications.
Add its record implementing `IGameEvent` under that subsystem's SDK `Events/`
directory, with `EventName` matching the native publication. No extra subscription
methods or central event table are needed.
Only infrastructure shared by subsystems belongs in `game_events`. No transport
switch is needed.

## Other events found in the current headers

These are candidates, not events exposed by this module yet:

- `TESActivateEvent`: activation of an object and its activator.
- `TESHitEvent`: target, cause, weapon/projectile IDs and hit data.
- `TESContainerChangedEvent`: old/new container, object, count and reference IDs.
- `TESEquipEvent`: actor, object and equip/unequip flag; source discovery needs work.
- `TESQuestEvent::Event`: quest and change type; source discovery needs work.
- `TESObjectLoadedEvent`: object load/unload.
- `TESCellFullyLoadedEvent`: a cell completed loading, distinct from player entry.
- `TESLocationClearedEvent`: a location was cleared, distinct from entering it.
- `TESFurnitureEvent`, `TESHarvestEvent`, `TESMagicEffectApplyEvent`: furniture,
  harvesting and applied effects.
- F4SE also reports input readiness, plugin load phases and save deletion.

## Verification

From the repository root:

```powershell
xmake f -P native/tests/game_events -m releasedbg -y
xmake -P native/tests/game_events -j 2
xmake run -P native/tests/game_events F4Forge.GameEvents.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
.\build-mo2.ps1
```

The standalone native tests use the production runtime, lifecycle adapter and payload
translation/forwarding helpers with fake engine inputs. They do not invoke real engine
addresses. The .NET tests exercise all eleven wire contracts through the native
transport, subscription disposal, nullable payloads and subscriber error isolation.

**In-game verification is still required.** A successful build and automated tests do
not verify the engine's location payload or the Address Library vtable selection in a
running game. Check new game, successful/failed load, saving, player/NPC death, interior
and exterior cell transitions, BGSLocation changes within a cell, opening/closing menus,
and continued Papyrus location callbacks with the hook enabled.
