using F4Forge.Net.Events;

namespace F4Forge.Net.Player.Events;

/// <summary>The player's named BGSLocation changed, independently of cell boundaries.</summary>
/// <param name="PreviousLocationFormId">The old location ID, or zero when absent.</param>
/// <param name="NewLocationFormId">The new location ID, or zero when absent.</param>
public sealed record PlayerLocationChanged(uint PreviousLocationFormId, uint NewLocationFormId);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class PlayerEventExtensions
{
    /// <summary>The player's named BGSLocation changed, independently of cell boundaries.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnPlayerLocationChanged(this IEventChannel events, Action<PlayerLocationChanged> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.player.location.changed", handler);
    }
}
