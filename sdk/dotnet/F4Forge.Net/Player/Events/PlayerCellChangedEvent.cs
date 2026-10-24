using F4Forge.Net.Events;

namespace F4Forge.Net.Player.Events;

/// <summary>The player entered a different cell.</summary>
/// <param name="PreviousCellFormId">The previous cell ID, or zero after a tracking reset.</param>
/// <param name="NewCellFormId">The cell the player entered.</param>
public sealed record PlayerCellChangedEvent(uint PreviousCellFormId, uint NewCellFormId) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.player.cell.changed";
}
