using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>The engine returned from its save routine; this does not certify save success.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveSavedEvent(string? SaveName) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.save.saved";
}
