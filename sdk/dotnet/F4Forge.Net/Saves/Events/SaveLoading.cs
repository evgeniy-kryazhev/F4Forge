using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>Loading a saved game is about to begin.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveLoading(string? SaveName);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class SaveEventExtensions
{
    /// <summary>Loading a saved game is about to begin.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnSaveLoading(this IEventChannel events, Action<SaveLoading> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.loading", handler);
    }
}
