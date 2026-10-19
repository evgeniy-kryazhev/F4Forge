using F4Forge.Net.Events;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Diagnostics;

internal sealed class OwnedChannels(string modId, IMessageChannel messages, IEventChannel events, ModLifetime lifetime) : IMessageChannel, IEventChannel
{
    public TResult Call<TResult>(string method, object? arguments)
    {
        if (method.StartsWith("diagnostics.", StringComparison.Ordinal))
        {
            return messages.Call<TResult>(method, arguments);
        }
        return DiagnosticTrace.Run(modId, "message.call", method, messages, () => messages.Call<TResult>(method, arguments));
    }

    public IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler)
    {
        using var ownership = DiagnosticTrace.Bind(modId, lifetime);
        return messages.Register(method, handler);
    }

    public void Publish<T>(string name, T data)
    {
        DiagnosticTrace.Run(modId, "event.publish", name, messages, () =>
        {
            events.Publish(name, data);
            return true;
        });
    }

    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        using var ownership = DiagnosticTrace.Bind(modId, lifetime);
        return events.Subscribe(name, handler);
    }

    public IDisposable Subscribe<T>(string name, Func<T, Task> handler)
    {
        using var ownership = DiagnosticTrace.Bind(modId, lifetime);
        return events.Subscribe(name, handler);
    }

    public IDisposable Subscribe<T>(string name, Func<T, CancellationToken, Task> handler)
    {
        using var ownership = DiagnosticTrace.Bind(modId, lifetime);
        return events.Subscribe(name, handler);
    }
}
