using F4Forge.Net.Callbacks;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Interception;

/// <summary>Mod-owned synchronous interceptors, separate from notifications.</summary>
public sealed class InterceptorService
{
    private readonly IMessageChannel _messages;
    private readonly IMessageChannel _cleanup;
    private readonly ModLifetime _lifetime;

    internal InterceptorService(IMessageChannel messages, IMessageChannel cleanup, ModLifetime lifetime)
    {
        _messages = messages;
        _cleanup = cleanup;
        _lifetime = lifetime;
    }

    /// <summary>Registers a synchronous decision validated by its native game adapter.</summary>
    /// <typeparam name="TContext">Serialized input defined by the adapter.</typeparam>
    /// <typeparam name="TDecision">Serialized decision defined by the adapter.</typeparam>
    /// <param name="name">The interception point exposed by a native module.</param>
    /// <param name="handler">Synchronous callback; never return an asynchronous operation.</param>
    /// <returns>A registration disposed automatically on startup failure or shutdown.</returns>
    public IDisposable Register<TContext, TDecision>(string name, Func<TContext, TDecision> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        using var operation = _lifetime.Enter();
        var method = "interceptor.callback." + Guid.NewGuid().ToString("N");
        var callback = _messages.Register(method, handler);
        Registration? registration = null;
        try
        {
            var id = _messages.Call<ulong>("interceptors.register", new { Name = name, Method = method });
            registration = new Registration(_cleanup, id, callback);
            _lifetime.Own(registration);
            return registration;
        }
        catch
        {
            if (registration is not null)
            {
                registration.Dispose();
            }
            else
            {
                callback.Dispose();
            }
            throw;
        }
    }

    /// <summary>Adapts a synchronous mutable context and completes it on every exit path.</summary>
    /// <typeparam name="TInput">Serialized input defined by the native adapter.</typeparam>
    /// <typeparam name="TContext">Mutable context exposed to the mod.</typeparam>
    /// <typeparam name="TDecision">Serialized result validated by the native adapter.</typeparam>
    /// <param name="name">The interception point exposed by a native module.</param>
    /// <param name="handler">Synchronous mod callback.</param>
    /// <param name="createContext">Creates one fresh context from the input.</param>
    /// <param name="createDecision">Copies the decision before the context is completed.</param>
    /// <returns>A registration owned by the mod.</returns>
    public IDisposable Register<TInput, TContext, TDecision>(string name, Action<TContext> handler,
        Func<TInput, TContext> createContext, Func<TContext, TDecision> createDecision)
        where TContext : InvocationContext
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(createContext);
        ArgumentNullException.ThrowIfNull(createDecision);
        return Register<TInput, TDecision>(name, input =>
            InvocationContext.Invoke(createContext(input), handler, createDecision));
    }

    private sealed class Registration(IMessageChannel cleanup, ulong id, IDisposable callback) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            try
            {
                cleanup.Call<bool>("interceptors.remove", new { Registration = id });
            }
            finally
            {
                callback.Dispose();
            }
        }
    }
}
