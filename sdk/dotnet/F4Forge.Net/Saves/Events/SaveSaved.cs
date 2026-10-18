using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>The engine returned from its save routine; this does not certify save success.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveSaved(string? SaveName);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class SaveEventExtensions
{
    /// <summary>The engine returned from its save routine; this does not certify save success.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnSaveSaved(this IEventChannel events, Action<SaveSaved> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.saved", handler);
    }
}
