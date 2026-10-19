using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>The runtime finished its one-time startup.</summary>
public sealed record RuntimeLoaded();

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class GameLifecycleEventExtensions
{
    /// <summary>The runtime finished its one-time startup.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnRuntimeLoaded(this IEventChannel events, Action<RuntimeLoaded> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.runtime.loaded", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnRuntimeLoaded(this IEventChannel events, Func<RuntimeLoaded, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.runtime.loaded", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnRuntimeLoaded(this IEventChannel events, Func<RuntimeLoaded, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.runtime.loaded", handler);
    }
}
