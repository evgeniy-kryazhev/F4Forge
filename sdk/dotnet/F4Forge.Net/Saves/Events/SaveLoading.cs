using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>Loading a saved game is about to begin.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveLoading(string? SaveName) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.save.loading";
}
