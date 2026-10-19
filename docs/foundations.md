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
using F4Forge.Net.Events;
using F4Forge.Net.Logging;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Player.Queries;

context.Events.Subscribe<ActorDied>(notification =>
{
    var actor = context.Objects.Query(new FormById<Actor>(notification.ActorFormId));
    if (actor is not null)
    {
        var snapshot = actor.GetSnapshot();
        context.LogInfo($"Actor {actor.FormId:X8}, cell {snapshot.CellFormId:X8}");
    }
});
```

All lookups use `context.Objects.Query(query)`. The query declares its native method, wire arguments and expected view through `IFormQuery<TForm>`; the common executor handles transport, missing forms and type validation. Query metadata is never serialized with the arguments.

```csharp
Actor? player = context.Objects.Query(new GetPlayer());
var weapon = context.Objects.Query(new FormByPlugin<Weapon>("Example.esp", 0x123));
var form = context.Objects.Query(new FormByEditorId<Form>("ExampleWeapon"));
```

`GetPlayer` belongs to `Player/Queries`; generic lookups belong to `Objects/Queries`. `FormById<TForm>`, `FormByPlugin<TForm>` and `FormByEditorId<TForm>` support built-in and external views. Missing forms return `null`; incompatible types throw `InvalidCastException`. Editor IDs work only when retained by the game's runtime index. Plugin-local lookup supports regular and light plugins. The previous `Get`, `Resolve`, `Find` and `GetPlayer` service methods are replaced by these query contracts.

Add a module-specific lookup by implementing `IFormQuery<TForm>` in that module. Only `Arguments` crosses the channel, so it must contain exactly the native method's wire fields. For example, the player query supplies `forms.player` and null arguments with `Actor` as its result view. Unlike event names, query names are instance properties: `Query` infers its result type from `IFormQuery<TForm>` and supports parameterized query objects without reflection or a central method table.

`Form` describes any form. `Reference` adds a copied position and base/cell/location IDs. `Actor` and `ActorValue` enable `actor.GetValue(value, kind)` and `actor.SetBaseValue(value, amount)` for any actor-value definition, including mod-defined values. Base writes preserve damage and temporary modifiers. `0` means an absent linked object.

Operations are synchronous and allowed only on the engine's main thread, identified by `RE::Main::threadID`. Load notifications update readiness and session identity without changing thread ownership; they may arrive on a loader thread. Console handlers on the main thread can resolve the player and read snapshots through nested channel calls. Calls during loading, from other threads, after mod disposal or with a previous session's identity fail explicitly. Calls do not queue work or wait on another thread while holding the shared dispatch lane. Resolve wrappers again after loading/new game. Form IDs are not persistent handles; missing objects are checked again in C++. No native pointer is retained by a managed wrapper.

## Additive extensions

A custom view needs only a new class; no SDK type table changes:

```csharp
public sealed class Weapon : Form, IFormView<Weapon>
{
    private Weapon(Form source) : base(source) { }

    public new static bool Matches(FormSnapshot snapshot)
    {
        return snapshot.Type == "WEAP";
    }

    public new static Weapon Create(Form source)
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
