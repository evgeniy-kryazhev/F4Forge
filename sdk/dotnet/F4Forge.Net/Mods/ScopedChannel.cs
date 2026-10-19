using F4Forge.Net.Messaging;

namespace F4Forge.Net.Mods;

internal sealed class ScopedChannel(IMessageChannel channel, ModLifetime lifetime) : IMessageChannel, IDisposable
{
    // Used exclusively by synchronous detach while the native host is alive.
    internal IMessageChannel Transport => channel;

    public TResult Call<TResult>(string method, object? arguments)
    {
        using var operation = lifetime.Enter();
        return channel.Call<TResult>(method, arguments);
    }

    public IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler)
    {
        using var operation = lifetime.Enter();
        ArgumentNullException.ThrowIfNull(handler);
        var registration = channel.Register<TArguments, TResult>(method, arguments =>
        {
            using var invocation = lifetime.Enter();
            return handler(arguments);
        });
        return lifetime.Own(registration);
    }

    public void Dispose()
    {
        lifetime.Close();
    }
}
