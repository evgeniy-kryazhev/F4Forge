using F4Forge.Net.Diagnostics;
using F4Forge.Net.Events;

namespace F4Forge.Net.Mods;

internal sealed class ScopedEvents(IEventChannel events, ModLifetime lifetime,
    Action<DiagnosticRecord>? record = null, Action<string>? report = null) : IEventChannel, IDisposable
{
    public void Publish<T>(string name, T data)
    {
        using var operation = lifetime.Enter();
        events.Publish(name, data);
    }

    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        using var operation = lifetime.Enter();
        ArgumentNullException.ThrowIfNull(handler);
        var subscription = events.Subscribe<T>(name, data =>
        {
            using var invocation = lifetime.Enter();
            handler(data);
        });
        return lifetime.Own(subscription);
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
        return lifetime.Own(notification);
    }

    public void Dispose()
    {
        lifetime.Close();
    }
}
