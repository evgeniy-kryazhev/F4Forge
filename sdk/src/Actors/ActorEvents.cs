using System.Runtime.Loader;

namespace F4Forge.Actors;

using F4Forge.Bridge;

/// <summary>Gameplay events for actors.</summary>
public static class ActorEvents
{
    private static readonly Lock Gate = new();
    private static Action<Actor>? _deathHandlers;
    private static IDisposable? _deathSubscription;

    /// <summary>
    /// Raised synchronously when an actor has died. The actor is borrowed for the callback.
    /// Handlers defined in a collectible plugin are removed when its load context unloads.
    /// </summary>
    public static event Action<Actor> OnDeath
    {
        add
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (Gate)
            {
                _deathSubscription ??= Bridge.On<Events.TESDeathEvent>(Events.TESDeathEvent.Name, OnNativeDeath);
                foreach (var handler in Delegate.EnumerateInvocationList(value))
                {
                    var owner = AssemblyLoadContext.GetLoadContext(handler.Method.Module.Assembly);
                    if (owner is { IsCollectible: true })
                    {
                        // The bridge sees the SDK adapter. Track the original plugin handlers here.
                        owner.Unloading -= OnPluginUnloading;
                        owner.Unloading += OnPluginUnloading;
                    }
                }
                _deathHandlers += value;
            }
        }
        remove
        {
            lock (Gate)
            {
                _deathHandlers -= value;
                DisconnectIfEmpty();
            }
        }
    }

    private static void OnNativeDeath(object? sender, Events.TESDeathEvent data)
    {
        if (!data.Dead || Actor.FromHandle(data.ActorDying) is not { } actor)
        {
            return;
        }
        Action<Actor>? handlers;
        lock (Gate)
        {
            handlers = _deathHandlers;
        }
        List<Exception>? failures = null;
        foreach (var handler in Delegate.EnumerateInvocationList(handlers))
        {
            try
            {
                handler(actor);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }
        if (failures is not null)
        {
            throw new AggregateException("Actor death handlers failed.", failures);
        }
    }

    private static void OnPluginUnloading(AssemblyLoadContext context)
    {
        lock (Gate)
        {
            foreach (var handler in Delegate.EnumerateInvocationList(_deathHandlers))
            {
                if (AssemblyLoadContext.GetLoadContext(handler.Method.Module.Assembly) == context)
                {
                    _deathHandlers -= handler;
                }
            }
            DisconnectIfEmpty();
        }
    }

    private static void DisconnectIfEmpty()
    {
        if (_deathHandlers is null)
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
        }
    }
}
