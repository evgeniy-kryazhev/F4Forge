namespace F4Forge.Net.Events;

/// <summary>Synchronous named events, delivered to current subscribers without replay.</summary>
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
}
