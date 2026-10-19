namespace F4Forge.Net.Events;

/// <summary>Named notifications, delivered without replay or engine decisions.</summary>
public interface IEventChannel
{
    /// <summary>Publishes an event in the caller's thread; subscriber failures are logged.</summary>
    /// <param name="name">Event name.</param>
    /// <param name="data">Event data supported by the shared serializer.</param>
    void Publish<T>(string name, T data);

    /// <summary>Subscribes in registration order until disposal or mod shutdown.</summary>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Synchronous callback; never wait on a thread that needs the channel.</param>
    /// <returns>An owned subscription. Disposal waits for active delivery except when called from that delivery.</returns>
    IDisposable Subscribe<T>(string name, Action<T> handler);

    /// <summary>Starts an async notification without waiting for completion.</summary>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Callback invoked in the publisher's thread until its first incomplete await.</param>
    /// <returns>A subscription disposed automatically when its mod stops.</returns>
    IDisposable Subscribe<T>(string name, Func<T, Task> handler);

    /// <summary>Starts a cancellable async notification without delaying the engine.</summary>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Callback with a token cancelled on unsubscription or shutdown.</param>
    /// <returns>A subscription; disposal requests cancellation without waiting for asynchronous completion.</returns>
    IDisposable Subscribe<T>(string name, Func<T, CancellationToken, Task> handler);
}
