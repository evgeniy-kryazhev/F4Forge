using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>A new game was created; the playable world may still be loading.</summary>
public sealed record NewGameEvent() : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.new";
}
