using F4Forge.Net.Events;

namespace F4Forge.Net.Mods;

internal sealed class ScopedEvents(IEventChannel events, ModLifetime lifetime) : IEventChannel, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private volatile bool _disposed;

    public void Publish<T>(string name, T data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        events.Publish(name, data);
    }

    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        var subscription = events.Subscribe<T>(name, data =>
        {
            lifetime.ThrowIfClosed();
            handler(data);
        });
        lock (_gate)
        {
            if (!_disposed)
            {
                _subscriptions.Add(subscription);
                return subscription;
            }
        }
        subscription.Dispose();
        throw new ObjectDisposedException(nameof(ScopedEvents));
    }

    public void Dispose()
    {
        lifetime.Close();
        IDisposable[] subscriptions;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
        }
        foreach (var subscription in subscriptions.Reverse())
        {
            subscription.Dispose();
        }
    }
}
