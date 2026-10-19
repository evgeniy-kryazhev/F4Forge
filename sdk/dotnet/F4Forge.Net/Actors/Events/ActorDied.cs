using F4Forge.Net.Events;

namespace F4Forge.Net.Actors.Events;

/// <summary>An actor completed dying; preliminary dying notifications are excluded.</summary>
/// <param name="ActorFormId">The dying actor reference ID.</param>
/// <param name="KillerFormId">The killer reference ID, or zero when absent.</param>
/// <param name="IsPlayer">Whether the dying actor is the player.</param>
public sealed record ActorDied(uint ActorFormId, uint KillerFormId, bool IsPlayer);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class ActorEventExtensions
{
    /// <summary>An actor completed dying; preliminary dying notifications are excluded.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnActorDied(this IEventChannel events, Action<ActorDied> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.actor.died", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnActorDied(this IEventChannel events, Func<ActorDied, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.actor.died", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnActorDied(this IEventChannel events, Func<ActorDied, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.actor.died", handler);
    }
}
