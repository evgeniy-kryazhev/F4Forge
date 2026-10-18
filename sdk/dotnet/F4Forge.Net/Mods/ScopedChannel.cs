using System.Text.Json;
using F4Forge.Net.Messaging;

namespace F4Forge.Net.Mods;

internal sealed class ScopedChannel(IMessageChannel channel) : IMessageChannel, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _registrations = [];
    private volatile bool _disposed;

    public TResult Call<TResult>(string method, object? arguments)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return channel.Call<TResult>(method, arguments);
    }

    public IDisposable Register(string method, Func<JsonElement, object?> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var registration = channel.Register(method, handler);
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
