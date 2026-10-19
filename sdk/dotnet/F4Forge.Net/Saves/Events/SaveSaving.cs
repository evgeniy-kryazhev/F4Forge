using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>The engine is about to save the game.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveSaving(string? SaveName) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.save.saving";
}
