using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>An attempt to load a saved game has finished.</summary>
/// <param name="SaveName">The name from the corresponding loading notification, if available.</param>
/// <param name="Success">Whether the engine reported a successful load.</param>
public sealed record SaveLoadedEvent(string? SaveName, bool Success) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.save.loaded";
}
