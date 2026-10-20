# Dynamic Papyrus Bridge

`context.Papyrus` calls functions and accesses properties by name. Their types come from VM metadata: no separate class or ABI binding is needed for each method in another mod. The existing channel and its `IDataSerializer` are used; interop exports and the central method table remain unchanged.

## Global Functions and UI

```csharp
var result = await context.Papyrus.CallStaticAsync(
    "PrismaUI", "CreateView", ["my-overlay", "my-mod/index.html"], cancellationToken);
if (result.Kind == "bool" && result.Boolean)
{
    await context.Papyrus.CallStaticAsync(
        "PrismaUI", "Show", ["my-overlay"], cancellationToken);
}
```

These are published functions in PrismaUI's Papyrus API; the mod itself must be installed and must register its native functions. F4Forge does not include PrismaUI or its renderer.

The same mechanism calls UI functions registered by F4SE:

```csharp
var isOpen = await context.Papyrus.CallStaticAsync(
    "UI", "IsMenuOpen", ["HUDMenu"], cancellationToken);

var result = await context.Papyrus.CallStaticAsync(
    "UI", "Invoke", ["HUDMenu", "root.myFunction", new object?[] { 42, "text" }], cancellationToken);
```

`root.myFunction` is an example path: the actual function must exist in the open menu. The call path is C# → Papyrus VM → F4SE native function → Scaleform. The bridge does not infer FallUI's internal functions or expose an arbitrary DLL's C++ API by name.

## Methods and Properties

```csharp
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;

var quest = context.Objects.Query(new FormByPlugin<Form>("Example.esp", 0x123));
if (quest is not null)
{
    var script = context.Papyrus.Object(quest, "ExampleQuestScript");
    var count = await context.Papyrus.CallAsync(script, "GetCount", cancellationToken: cancellationToken);
    var property = await context.Papyrus.GetPropertyAsync(script, "Counter", cancellationToken);
    await context.Papyrus.SetPropertyAsync(script, "Counter", 10, cancellationToken);
}
```

Retrieving a form through `Objects.Query` requires the game thread; after `await`, use `GameThread.RunAsync` for this step. The Papyrus client itself queues dispatch and result conversion through F4SE and can be called from any managed thread. Custom scripts must already be attached to the form; the bridge does not attach them automatically. A native form representation, such as `Actor` or `Quest`, can be created through the standard VM mechanism.

Properties are read and modified through the VM, preserving getter/setter behavior. Direct writes to internal variables and subscriptions to other mods' custom events are not included in this release. For America Rising 2, obtain file, form, script, and function names from the resources of the specific installed version; the core contains no special AR2 names.

## Values

Arguments accept `null`, `bool`, `int`, finite `float`/`double`, `string`, forms belonging to the current mod, `PapyrusObject`, arrays, and `PapyrusStruct`. Nested values are limited to a depth of 32. Array types are inferred from the function signature; inside `Var`, a new array without an explicit signature is passed as `Var[]`. The returned `PapyrusValue` preserves the array element type in `TypeName`, including for empty arrays.

```csharp
using F4Forge.Net.Papyrus;

var data = new PapyrusStruct("UI#MenuData", new Dictionary<string, object?>
{
    ["depth"] = 6
});
```

Use the VM struct name with `#`; fields are validated against metadata. Omitted fields retain the values created by the VM. Omitted function arguments are left to the VM's invocation mechanism; a missing required argument should cause the VM to reject the call rather than F4Forge supplying a value automatically.

The result is a `PapyrusValue`: `Kind` identifies the populated field among `Boolean`, `Integer`, `Number`, `Text`, `Reference`, `Items`, and `Fields`. `none` also represents the result of a function with no return value. A returned `Reference` can be passed to subsequent client calls; it is an opaque token, not a memory address. Tokens belong to one client and session and are released when the mod stops or the save changes. Each client stores at most 4096 returned references.

## Lifetime and Errors

Each call has a 30-second timeout. `CancellationToken` cancels the wait and work that has not yet started. A Papyrus stack that is already running is not interrupted; its late result is discarded. The client does not block the game thread, and its continuations run asynchronously.

Calls require game data to be ready and save loading to be complete. An unknown script, function, or property, an incompatible argument, VM rejection, or a stale reference causes the task to fail. The VM reports some script execution errors only in its own log without invoking the callback; the timeout applies in those cases. VM bridge errors are reported as `MessageException` with the `papyrus_failed` code; requests rejected by the channel itself retain the channel's error code.

Stopping a mod synchronously closes its native scope. Managed tasks remain tracked by the existing lifetime mechanism until completion. The native queue and VM callbacks hold no pointers to the managed mod. Buffers are copied before the callback returns; the same serializer and shared transport message size limit apply.

## Optional C# Wrapper

A wrapper is an ordinary mod library that calls this same API. For example, a `ShowAsync(name)` method can delegate to `CallStaticAsync("PrismaUI", "Show", [name])`. This requires no new methods, query types, or dependencies in F4Forge. The low-level API remains available without a wrapper.

## Verification

```powershell
xmake -P native/tests/papyrus -j 2
xmake run -P native/tests/papyrus F4Forge.Papyrus.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Isolated tests cover transport, correlation, errors, cancellation, closure, session changes, repeated callbacks, and alternative serialization. The module compiles against the real CommonLib interfaces, but isolated tests do not run the Fallout 4 VM.

**Integration with a running VM has not yet been verified in the game.** For manual verification, compile `tests/papyrus/F4ForgeBridgeProbe.psc` with the standard Creation Kit compiler into a separate test mod. Static functions are available without attachment to an object. Attach the script to a test quest to check properties and methods. Check `Add`, `Echo`, arrays, structs, the latent `WaitAndReturn` call, properties, and rejection after loading a save. The script does not depend on the F4Forge API and requires no changes to other mods.
