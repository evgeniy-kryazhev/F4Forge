using F4Forge.Net.Events;

namespace F4Forge.Net.Game.Events;

/// <summary>The runtime finished its one-time startup.</summary>
public sealed record RuntimeLoaded() : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.runtime.loaded";
}
