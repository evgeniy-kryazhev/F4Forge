# Diagnostics, hook checks and game objects

These modules use the existing message channel and its `IDataSerializer`. They add no ABI exports, central dispatch switches or native pointers to the SDK. Core and SDK remain the two distribution DLLs.

## Mod diagnostics

`context.Diagnostics.ModId` is the absolute mod DLL path. Calls, publications, managed handlers and loader stages record the owner, operation/parent IDs, UTC time, thread, duration and outcome. Failures include exception type, message and managed stack; native diagnostics forward failures to the framework log.

```csharp
var recent = context.Diagnostics.GetOperations(maximum: 32);
var hooks = context.Diagnostics.GetHooks();
```

The host retains the newest 256 operations across all mods. Queries filter by the current mod and may return fewer records to fit the transport limit. Exception text is truncated. Diagnostic failures cannot replace the original result or exception. Records retain values and strings; subscriptions remain owned by the mod. Startup failure triggers the usual rollback, with separate records for each cleanup stage. This is managed operation diagnostics, not an OS crash-dump facility.

## Hook validation

`native/modules/diagnostics/` provides readable-memory, executable-target and vtable-slot checks, a byte-pattern matcher and `CheckAndInstall`. Hook states are `checking`, `active`, `inactive` and `failed`; failures are logged.

Console installation checks its Address Library target before MinHook relocates instructions. Location installation checks the original vtable target and verifies the slot after writing. Existing original-call forwarding is preserved. No guessed byte signature or private address database is introduced. These checks establish memory and installation preconditions; they do not prove game ABI semantics or compatibility with every other plugin.

## Typed objects

```csharp
using F4Forge.Net.Actors.Events;
using F4Forge.Net.Logging;
using F4Forge.Net.Objects;

context.Events.OnActorDied(notification =>
{
    var actor = context.Objects.Get<Actor>(notification.ActorFormId);
    if (actor is not null)
    {
        var snapshot = actor.GetSnapshot();
        context.LogInfo($"Actor {actor.FormId:X8}, cell {snapshot.CellFormId:X8}");
    }
});
```

`Get(id)`, `Resolve(pluginFilename, localId)` and `Find(editorId)` resolve arbitrary forms. Missing forms return `null`; editor IDs work only when retained by the game's runtime index. Plugin-local lookup supports regular and light plugins. `GetPlayer()` returns an `Actor` view.

`Form` describes any form. `Reference` adds a copied position and base/cell/location IDs. `Actor` and `ActorValue` enable `actor.GetValue(value, kind)` and `actor.SetBaseValue(value, amount)` for any actor-value definition, including mod-defined values. Base writes preserve damage and temporary modifiers. `0` means an absent linked object.

Operations are synchronous and allowed only on the thread captured when game data becomes ready. Calls during loading, from other threads, after mod disposal or with a previous session's identity fail explicitly. Resolve wrappers again after loading/new game. Form IDs are not persistent handles; missing objects are checked again in C++. No native pointer is retained by a managed wrapper.

## Additive extensions

A custom view needs only a new class; no SDK type table changes:

```csharp
public sealed class Weapon : Form, IFormView<Weapon>
{
    private Weapon(Form source) : base(source) { }

    public static bool Matches(FormSnapshot snapshot)
    {
        return snapshot.Type == "WEAP";
    }

    public static Weapon Create(Form source)
    {
        return new Weapon(source);
    }
}
```

New behavior belongs in an extension class calling `form.Messages` and a self-registering native component. Validate the form ID, session, thread and actual engine type before touching game memory; use the shared serializer for all arguments and results.

## Verification

Run the existing root tests and managed suite, plus:

```powershell
xmake -P native/tests/foundation -j 2
xmake run -P native/tests/foundation F4Forge.Foundation.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Game-facing calls and hook integration are compiled and exercised through isolated test backends. They still require a Fallout 4 launch: verify data readiness, player/reference reads, actor-value access, save loading/session rejection, console forwarding and location changes.
