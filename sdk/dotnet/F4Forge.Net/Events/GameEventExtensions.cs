namespace F4Forge.Net.Events;

/// <summary>Typed notifications without a separate subscription wrapper for every event.</summary>
public static class GameEventExtensions
{
    /// <summary>Subscribes synchronously using the notification type's channel name.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Callback invoked in the publisher's thread.</param>
    /// <returns>A subscription owned by the mod.</returns>
    public static IDisposable Subscribe<T>(this IEventChannel events, Action<T> handler) where T : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe(T.EventName, handler);
    }

    /// <summary>Subscribes asynchronously without waiting for the callback's completion.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Callback tracked through its asynchronous completion.</param>
    /// <returns>A subscription owned by the mod.</returns>
    public static IDisposable Subscribe<T>(this IEventChannel events, Func<T, Task> handler) where T : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe(T.EventName, handler);
    }

    /// <summary>Subscribes asynchronously with cancellation on unsubscription or shutdown.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="handler">Callback receiving its subscription's cancellation token.</param>
    /// <returns>A subscription owned by the mod.</returns>
    public static IDisposable Subscribe<T>(this IEventChannel events, Func<T, CancellationToken, Task> handler)
        where T : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(handler);
        return events.Subscribe(T.EventName, handler);
    }

    /// <summary>Publishes a notification using the same name as typed subscriptions.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="events">The mod-owned event channel.</param>
    /// <param name="notification">Copied notification data supported by the shared serializer.</param>
    public static void Publish<T>(this IEventChannel events, T notification) where T : IGameEvent
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(notification);
        events.Publish(T.EventName, notification);
    }
}
