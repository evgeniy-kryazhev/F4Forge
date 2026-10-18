using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>An attempt to load a saved game has finished.</summary>
/// <param name="SaveName">The name from the corresponding loading notification, if available.</param>
/// <param name="Success">Whether the engine reported a successful load.</param>
public sealed record SaveLoaded(string? SaveName, bool Success);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class SaveEventExtensions
{
    /// <summary>An attempt to load a saved game has finished.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnSaveLoaded(this IEventChannel events, Action<SaveLoaded> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.loaded", handler);
    }
}
