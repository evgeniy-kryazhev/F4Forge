using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>The engine changed the readiness of its game data.</summary>
/// <param name="Ready">Whether game data is ready.</param>
public sealed record GameDataReadyEvent(bool Ready) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.data.ready";
}
