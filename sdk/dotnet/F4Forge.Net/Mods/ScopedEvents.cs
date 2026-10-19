using F4Forge.Net.Events;

namespace F4Forge.Net.Mods;

internal sealed class ScopedEvents(IEventChannel events, ModLifetime lifetime,
    Action<F4Forge.Net.Diagnostics.DiagnosticRecord>? record = null, Action<string>? report = null) : IEventChannel, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _subscriptions = [];
    private volatile bool _disposed;

    public void Publish<T>(string name, T data)
    {
        using var operation = lifetime.Enter();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        events.Publish(name, data);
    }

    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        using var operation = lifetime.Enter();
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        var subscription = events.Subscribe<T>(name, data =>
        {
            using var invocation = lifetime.Enter();
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

    public IDisposable Subscribe<T>(string name, Func<T, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe<T>(name, (value, _) => handler(value));
    }

    public IDisposable Subscribe<T>(string name, Func<T, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = lifetime.Enter();
        var notification = new AsyncNotification<T>(lifetime, name, handler,
            callback => events.Subscribe(name, callback), record ?? (_ => { }), report ?? Console.Error.WriteLine);
        lock (_gate)
        {
            if (!_disposed)
            {
                _subscriptions.Add(notification);
                return notification;
            }
        }
        notification.Dispose();
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
