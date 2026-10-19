using F4Forge.Net.Events;

namespace F4Forge.Net.UI.Events;

/// <summary>A menu opened or closed.</summary>
/// <param name="MenuName">The engine menu name.</param>
/// <param name="Opening">True for opening; false for closing.</param>
public sealed record MenuChanged(string MenuName, bool Opening);

/// <summary>Typed subscriptions to native game events through the shared event channel.</summary>
public static partial class UiEventExtensions
{
    /// <summary>A menu opened or closed.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">A synchronous callback in the engine publisher's thread.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    public static IDisposable OnMenuChanged(this IEventChannel events, Action<MenuChanged> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.menu.changed", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnMenuChanged(this IEventChannel events, Func<MenuChanged, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.menu.changed", handler);
    }

    /// <summary>Subscribes an asynchronous notification without delaying the engine.</summary>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Asynchronous callback; use GameThread.RunAsync for engine access after await.</param>
    /// <returns>A subscription cancelled automatically when its mod stops.</returns>
    public static IDisposable OnMenuChanged(this IEventChannel events, Func<MenuChanged, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe("game.menu.changed", handler);
    }
}
