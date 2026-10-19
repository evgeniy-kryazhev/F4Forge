using F4Forge.Net.Events;

namespace F4Forge.Net.Player.Events;

/// <summary>The player entered a different cell.</summary>
/// <param name="PreviousCellFormId">The previous cell ID, or zero after a tracking reset.</param>
/// <param name="NewCellFormId">The cell the player entered.</param>
public sealed record PlayerCellChanged(uint PreviousCellFormId, uint NewCellFormId);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class PlayerEventExtensions
{
    /// <summary>The player entered a different cell.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnPlayerCellChanged(this IEventChannel events, Action<PlayerCellChanged> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.player.cell.changed", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnPlayerCellChanged(this IEventChannel events, Func<PlayerCellChanged, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.player.cell.changed", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnPlayerCellChanged(this IEventChannel events, Func<PlayerCellChanged, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.player.cell.changed", handler);
    }
}
