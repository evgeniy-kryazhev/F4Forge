using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>The engine changed the readiness of its game data.</summary>
/// <param name="Ready">Whether game data is ready.</param>
public sealed record GameDataReady(bool Ready);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class GameLifecycleEventExtensions
{
    /// <summary>The engine changed the readiness of its game data.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnGameDataReady(this IEventChannel events, Action<GameDataReady> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.data.ready", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnGameDataReady(this IEventChannel events, Func<GameDataReady, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.data.ready", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnGameDataReady(this IEventChannel events, Func<GameDataReady, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.data.ready", handler);
    }
}
