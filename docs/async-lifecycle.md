# Notifications, Interceptors, and async/await

`IMod` implements `Task StartAsync(ModContext, CancellationToken)` and
`ValueTask DisposeAsync()`. The old `Start` and `IDisposable` contracts are not supported:
mods must be rebuilt. The new contract is available starting with version 0.3.0.

## Startup and Shutdown

The loader discovers DLLs in path order and starts each mod until its first
incomplete `await`, then continues starting the others. Registrations are available
immediately. Events published before registration are not replayed. A startup exception removes
registrations and starts cleanup for that mod only.

The SDK tracks startup, asynchronous notification, and game dispatcher tasks.
Mods must finish any background tasks they create themselves
in `DisposeAsync`. Do not use `async void` or leave tasks unobserved.

During shutdown, the SDK rejects new operations, removes native registrations, and
requests cancellation. `Bootstrap.Stop` then returns without waiting for
asynchronous completion. The SDK waits for tracked tasks, calls `DisposeAsync`
outside the game thread, and unloads the `AssemblyLoadContext`. Context services are already
unavailable inside `DisposeAsync`; use it to finish your own
tasks and release your own resources.

If a mod ignores cancellation, a warning appears after five seconds.
The instance and assembly are retained until completion; there is no forced
unload. Late diagnostic entries go to the managed log.
Restarting the SDK before cleanup completes is rejected.

## Notifications

Typed subscriptions use `context.Events.Subscribe<TEvent>(handler)`.
The event type implements `IGameEvent` and defines a static `EventName`; separate
`OnSaveLoaded`, `OnActorDied`, and other `On…` methods are no longer needed.
Three shared overloads support:

```csharp
Action<T>
Func<T, Task>
Func<T, CancellationToken, Task>
```

Adding a new notification requires only one definition:

```csharp
using F4Forge.Net.Events;

public sealed record InventoryChanged(uint OwnerFormId) : IGameEvent
{
    public static string EventName => "mymod.inventory.changed";
}
```

`context.Events.Publish(new InventoryChanged(ownerId))` uses the same name.
The `EventName` property is not included in serialized data. The low-level
`Subscribe<T>(name, handler)` and `Publish(name, data)` methods remain available for
dynamic names and contracts that do not implement `IGameEvent`.

The synchronous portion runs on the publisher's thread. Publication does not wait for
incomplete tasks. Invocation starts in registration order, but completion
order is not guaranteed. Repeated notifications may overlap.
The SDK observes errors after `await` and logs them with the owner and duration
of the entire operation. Cancellation through the subscription token is treated as normal completion.

Calling `Dispose` on a subscription prevents new invocations and cancels its token, but does not wait for
the handler's continuation. Mod shutdown waits for it to complete.
For a lambda consisting only of `throw`, specify `Action<T>` explicitly if you need
a synchronous contract; without an explicit type, the compiler may choose `Func<T, Task>`.

## Game Objects After await

A regular `await` does not guarantee a return to the game thread. Use
`context.GetSingleton<GameThread>().RunAsync` with a synchronous block:

```csharp
using System.Text.Json;
using F4Forge.Net.Events;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Objects;
using F4Forge.Net.Operations;
using F4Forge.Net.Player.Queries;
using F4Forge.Net.Saves.Events;
using F4Forge.Net.Singletons;

context.Events.Subscribe<SaveLoaded>(async (notification, cancellationToken) =>
{
    if (!notification.Success)
    {
        return;
    }

    var position = await context.GetSingleton<GameThread>().RunAsync(
        () =>
        {
            var player = context.GetSingleton<GameObjects>().Query(new GetPlayer());
            if (player is null)
            {
                return (Position?)null;
            }

            return context.Messages.Query(new GetReferenceSnapshot(player)).Position;
        },
        cancellationToken);

    await File.WriteAllTextAsync(
        Path.Combine(context.Directory, "player-position.json"),
        JsonSerializer.Serialize(position),
        cancellationToken);
});
```

On the game thread, the block runs immediately; otherwise, it runs through F4SE's
`AddTask`. The result or exception is returned through `Task`; continuations do not
run inside the native completion callback. Cancellation before the block starts prevents
execution; a block that has already started is not interrupted. The supplied block must be synchronous:
do not pass an asynchronous lambda to `RunAsync`.

World readiness and the object's generation are checked when the object is actually accessed.
Resolve objects again after loading a save. Do not call `.Wait()`,
`.Result`, or `.GetAwaiter().GetResult()` inside a channel handler.

## Interceptors

A notification reports something that has happened; an interceptor returns a decision before an action:

```csharp
using F4Forge.Net.Interception;
using static F4Forge.Net.Singletons.SingletonExtensions;

context.GetSingleton<GameInterception>().OnConsoleCommandExecuting(command =>
{
    if (command.Command == "mymod.help")
    {
        command.Command = "help";
    }
    if (command.Command == "mymod.disabled")
    {
        command.Skip = true;
    }
});
```

The context contains `OriginalCommand`, mutable `Command` and `Skip`, and
immutable `SelectedReferenceFormId`. It can be modified only
during the invocation. Handlers are synchronous and run in the mods' actual registration
order. Each receives the previous accepted decision. `Skip` ends the chain and
cancels both the mod command and the original game command.

If a handler throws or returns an invalid response, that handler's changes
are rolled back while earlier changes are preserved. Commands cannot contain NUL;
an empty string is allowed. The shared transport message limit of 1 MiB applies,
including the serialized envelope.

Execution order: interceptors → mod command router → original
game function. The original is called at most once. During host shutdown
or an infrastructure failure before a decision is obtained, the original input is preserved.

The shared native registry is in `native/modules/interception/`. It uses
`interceptors.register` and `interceptors.remove` and invokes registered
methods through the shared channel; the game adapter is responsible for validating the decision.
New typed contracts are added through SDK extension classes on top of
`GameInterception.Register<TInput, TContext, TDecision>`, without changing the shared
service. The extension defines the name, context creation, and decision copying.
`TContext` inherits from `InvocationContext` and calls `EnsureActive` before
changes. The shared adapter deactivates the context on success, handler exceptions,
and decision conversion failures. Console commands use the same mechanism.
The low-level `Register<TInput, TDecision>` is available for contracts without
a mutable context. It does not manage contexts itself.
The game dispatcher uses `threading.is_game_thread`, `threading.enqueue`, and
the `threading.execute` notification with an opaque task identifier.
All data passes through the shared `IDataSerializer`; the ABI and exports are unchanged.
