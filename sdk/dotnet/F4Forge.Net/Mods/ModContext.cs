using System.Runtime.CompilerServices;
using F4Forge.Net.Messaging;
using F4Forge.Net.Events;
using F4Forge.Net.Serialization;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Mods;

/// <summary>Directory, shared channels, serializer, and lifetime belonging to one mod.</summary>
public sealed class ModContext
{
    internal ModContext(string directory, IMessageChannel messages, IEventChannel events, IDataSerializer serializer,
        string? modId = null, ModLifetime? lifetime = null)
    {
        Directory = directory;
        ModId = Path.GetFullPath(modId ?? directory);
        Lifetime = lifetime ?? new ModLifetime();
        CleanupMessages = (messages as ScopedChannel)?.Transport ?? messages;
        var ownedChannels = new OwnedChannels(ModId, messages, events, Lifetime);
        Messages = ownedChannels;
        Events = ownedChannels;
        Serializer = serializer;
    }

    internal ModLifetime Lifetime { get; }
    internal string ModId { get; }
    // Detach must bypass closed mod channels while the native host is still alive.
    internal IMessageChannel CleanupMessages { get; }

    /// <summary>Absolute path to the mod directory.</summary>
    public string Directory { get; }

    /// <summary>Shared channel with registrations owned by this mod.</summary>
    public IMessageChannel Messages { get; }

    /// <summary>Shared events with subscriptions owned by this mod.</summary>
    public IEventChannel Events { get; }

    /// <summary>The same serializer used for messages, events, arguments and results.</summary>
    public IDataSerializer Serializer { get; }

    /// <summary>Gets this mod context's cached singleton proxy or subsystem facade.</summary>
    /// <typeparam name="T">A supported singleton proxy or subsystem facade.</typeparam>
    /// <returns>A typed proxy; the engine singleton need not exist yet.</returns>
    /// <remarks>Engine proxy members require the game thread. Native pointers are never cached.</remarks>
    public T GetSingleton<T>() where T : class, IGameSingleton<T>
    {
        // ConditionalWeakTable may invoke its factory concurrently. Only the stored
        // Lazy may construct a facade, because construction can register cleanup.
        var singleton = ProxyCache<T>.Instances.GetValue(this, static owner =>
        {
            return new Lazy<T>(() => T.Create(owner), LazyThreadSafetyMode.ExecutionAndPublication);
        });
        return singleton.Value;
    }

    /// <summary>Subscribes synchronously using the notification type's event name.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="handler">Callback invoked in the publisher's thread.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
    {
        return Events.Subscribe(handler);
    }

    /// <summary>Subscribes asynchronously using the notification type's event name.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="handler">Callback tracked through asynchronous completion.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(Func<T, Task> handler) where T : IGameEvent
    {
        return Events.Subscribe(handler);
    }

    /// <summary>Subscribes to a typed notification with cancellation on unsubscription or shutdown.</summary>
    /// <typeparam name="T">The notification contract.</typeparam>
    /// <param name="handler">Callback receiving its subscription's cancellation token.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IGameEvent
    {
        return Events.Subscribe(handler);
    }

    /// <summary>Subscribes synchronously to a named notification.</summary>
    /// <typeparam name="T">The notification payload.</typeparam>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Synchronous callback; never wait on a thread that needs the channel.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        return Events.Subscribe(name, handler);
    }

    /// <summary>Starts an asynchronous notification without waiting for completion.</summary>
    /// <typeparam name="T">The notification payload.</typeparam>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Callback invoked in the publisher's thread until its first incomplete await.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(string name, Func<T, Task> handler)
    {
        return Events.Subscribe(name, handler);
    }

    /// <summary>Subscribes to a named notification with cancellation on unsubscription or shutdown.</summary>
    /// <typeparam name="T">The notification payload.</typeparam>
    /// <param name="name">Event name.</param>
    /// <param name="handler">Callback receiving its subscription's cancellation token.</param>
    /// <returns>A subscription owned by this mod.</returns>
    public IDisposable Subscribe<T>(string name, Func<T, CancellationToken, Task> handler)
    {
        return Events.Subscribe(name, handler);
    }

    private static class ProxyCache<T> where T : class, IGameSingleton<T>
    {
        internal static readonly ConditionalWeakTable<ModContext, Lazy<T>> Instances = new();
    }
}
