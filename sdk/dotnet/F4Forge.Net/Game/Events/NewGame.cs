using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>A new game was created; the playable world may still be loading.</summary>
public sealed record NewGame();

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class GameLifecycleEventExtensions
{
    /// <summary>A new game was created; the playable world may still be loading.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnNewGame(this IEventChannel events, Action<NewGame> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.new", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnNewGame(this IEventChannel events, Func<NewGame, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.new", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnNewGame(this IEventChannel events, Func<NewGame, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.new", handler);
    }
}
