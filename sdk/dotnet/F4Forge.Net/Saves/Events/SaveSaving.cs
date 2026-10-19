using F4Forge.Net.Events;

namespace F4Forge.Net.Saves.Events;

/// <summary>The engine is about to save the game.</summary>
/// <param name="SaveName">The name supplied by F4SE, or null when unavailable.</param>
public sealed record SaveSaving(string? SaveName);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class SaveEventExtensions
{
    /// <summary>The engine is about to save the game.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnSaveSaving(this IEventChannel events, Action<SaveSaving> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.saving", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnSaveSaving(this IEventChannel events, Func<SaveSaving, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.saving", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnSaveSaving(this IEventChannel events, Func<SaveSaving, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.save.saving", handler);
    }
}
