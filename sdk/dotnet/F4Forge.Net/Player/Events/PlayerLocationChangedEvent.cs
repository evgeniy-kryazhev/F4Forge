using F4Forge.Net.Events;

namespace F4Forge.Net.Player.Events;

/// <summary>The player's named BGSLocation changed, independently of cell boundaries.</summary>
/// <param name="PreviousLocationFormId">The old location ID, or zero when absent.</param>
/// <param name="NewLocationFormId">The new location ID, or zero when absent.</param>
public sealed record PlayerLocationChangedEvent(uint PreviousLocationFormId, uint NewLocationFormId) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.player.location.changed";
}
