using F4Forge.Net.Messaging;

namespace F4Forge.Net.Mods;

internal sealed class ScopedChannel(IMessageChannel channel, ModLifetime lifetime) : IMessageChannel, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _registrations = [];
    private volatile bool _disposed;

    // Used exclusively by synchronous detach while the native host is alive.
    internal IMessageChannel Transport => channel;

    public TResult Call<TResult>(string method, object? arguments)
    {
        using var operation = lifetime.Enter();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        return channel.Call<TResult>(method, arguments);
    }

    public IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler)
    {
        using var operation = lifetime.Enter();
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lifetime.ThrowIfClosed();
        var registration = channel.Register<TArguments, TResult>(method, arguments =>
        {
            using var invocation = lifetime.Enter();
            return handler(arguments);
        });
        lock (_gate)
        {
            if (!_disposed)
            {
                _registrations.Add(registration);
                return registration;
            }
        }
        registration.Dispose();
        throw new ObjectDisposedException(nameof(ScopedChannel));
    }

    public void Dispose()
    {
        lifetime.Close();
        IDisposable[] registrations;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            registrations = _registrations.ToArray();
            _registrations.Clear();
        }
        // Never hold the scope lock while waiting for native callbacks to finish.
        foreach (var registration in registrations.Reverse())
        {
            registration.Dispose();
        }
    }
}
