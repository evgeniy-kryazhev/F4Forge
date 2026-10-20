# Typed Queries and Commands

The SDK separates reading data from changing state:

- `IQuery<TResult>` runs through `context.Messages.Query(query)`.
- `ICommand<TResult>` runs through `context.Messages.Execute(command)`.
- `IFormQuery<TForm>` extends a `FormSnapshot?` query; `context.Objects.Query(query)` also creates a typed object representation.

`IMessageChannel` remains the shared mechanism for calls and responses. Queries and commands describe
its calls through typed contracts, while `IEventChannel` delivers notifications.
The console API is in `F4Forge.Net.Console`; console handlers are not
`ICommand<TResult>` commands and retain the synchronous `AddHandler` contract.

The `Form`, `Reference`, and `Actor` types store object identity within the mod's scope.
`IFormType<T>` defines validation of the stored description through `Matches` and creation
of a typed wrapper through `Create`. The shared `Objects.Query` executor uses
this contract without a type table. `Is<T>()` and `As<T>()` check the stored description;
the native operation checks the current game state when the object is actually accessed.

The shared `IOperation` defines `MethodName`, `Arguments`, and an optional `Validate(IMessageChannel)` method. The executor validates arguments and the mod scope, then calls the existing channel. Only `Arguments` are serialized. The compiler infers the result type from the contract; there is no reflection, method table, or additional interop export.

```csharp
using F4Forge.Net.Actors.Commands;
using F4Forge.Net.Actors.Queries;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Operations;
using F4Forge.Net.Player.Queries;

var player = context.Objects.Query(new GetPlayer());
var health = context.Objects.Query(new FormByEditorId<ActorValue>("Health"));
if (player is not null && health is not null)
{
    float current = context.Messages.Query(new GetActorValue(player, health));
    ReferenceSnapshot snapshot = context.Messages.Query(new GetReferenceSnapshot(player));
    bool applied = context.Messages.Execute(new SetActorBaseValue(player, health, 150));
}
```

`Execute` returns the native handler's response. A `false` value is preserved and does not automatically become an exception. For an operation with a different response, specify the appropriate `TResult`, such as a custom record describing the outcome of a change. Argument validation exceptions occur before entering the transport. Native response errors are reported through `MessageException`; local serializer errors retain their original type. The shared executor does not catch or replace these exceptions.

## Adding a Feature

For arbitrary data, a record in the feature's module is sufficient:

```csharp
using F4Forge.Net.Operations;

public sealed record GetInventoryCount(uint OwnerFormId) : IQuery<int>
{
    public string MethodName => "mymod.inventory.count";
    public object Arguments => new { OwnerFormId };
}

public sealed record SetInventoryLimit(int Limit) : ICommand<bool>
{
    public string MethodName => "mymod.inventory.set_limit";
    public object Arguments => new { Limit };
}
```

Calls: `context.Messages.Query(new GetInventoryCount(ownerId))` and `context.Messages.Execute(new SetInventoryLimit(100))`. Handlers for these methods must be implemented and registered separately in the appropriate module. SDK contracts describe the call but do not implement the game feature.

Operations on an existing object inherit from `Objects.Operations.FormOperation`, which checks that the object belongs to the mod's channel. Actor value operations use `Actors.Operations.ActorValueOperation`, which also verifies that the actor and actor value definition share a session. The native handler resolves the ID again and checks the session generation, actual type, and world readiness. Do not serialize `Form`, the channel, or the entire operation object: pass only the required IDs, session, and parameters through `Arguments`.

Existing operations are organized by purpose:

| Module | Queries | Commands |
| --- | --- | --- |
| `Player/Queries` | `GetPlayer` | — |
| `Objects/Queries` | `FormById`, `FormByPlugin`, `FormByEditorId`, `DescribeForm`, `GetReferenceSnapshot` | — |
| `Actors` | `GetActorValue` | `SetActorBaseValue` |
| `Diagnostics/Queries` | `GetDiagnosticOperations`, `GetHookStatuses` | — |
| `Logging/Commands` | — | `WriteLog` |

The convenience methods `context.Diagnostics.GetOperations`, `GetHooks`, and `LogInfo`/`LogWarning`/`LogError` use these same contracts. The low-level `Messages.Call<TResult>(name, arguments)` remains available for dynamic methods and the transport's internal registration, dispatch, and diagnostic protocols.

## Game Thread and Lifetime

Both calls are synchronous and run on the caller's thread. They do not enqueue work, wait for other threads, or add asynchronous console commands. After `await`, use the existing dispatcher:

```csharp
bool applied = await context.GameThread.RunAsync(() =>
{
    var player = context.Objects.Query(new GetPlayer());
    var health = context.Objects.Query(new FormByEditorId<ActorValue>("Health"));
    if (player is null || health is null)
    {
        return false;
    }

    return context.Messages.Execute(new SetActorBaseValue(player, health, 150));
}, cancellationToken);
```

Cancellation before the block starts prevents execution; a synchronous operation that has already started is not forcibly interrupted. The mod's channel rejects calls after shutdown. A contract may hold a safe managed object representation, but not a native pointer. Resolve objects again after loading a save.
