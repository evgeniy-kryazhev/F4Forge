using System.Collections.Immutable;
using System.Runtime.Loader;

namespace F4Forge;

public static partial class Bridge
{
    /// <summary>
    /// Subscribes to an event by its case-sensitive name and exact payload type.
    /// Dispose the returned subscription to stop receiving future publications.
    /// Subscriptions to handlers defined in a plugin are removed when its load context unloads.
    /// </summary>
    public static IDisposable On<T>(string name, EventHandler<T> handler)
    {
        ValidateEventName(name);
        ArgumentNullException.ThrowIfNull(handler);
        return EventSubscription<T>.Add(name, handler);
    }

    /// <summary>
    /// Delivers an event synchronously on the publishing thread in subscription order.
    /// Changes to subscriptions take effect on the next publication.
    /// </summary>
    /// <exception cref="AggregateException">One or more handlers failed. All handlers were called.</exception>
    public static void Publish<T>(string name, object? sender, T args)
    {
        ValidateEventName(name);
        List<Exception>? failures = null;
        foreach (var subscription in EventSubscription<T>.Snapshot(name))
        {
            try
            {
                subscription.Handler(sender, args);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }
        if (failures is not null)
        {
            throw new AggregateException($"Handlers for event '{name}' failed.", failures);
        }
    }

    private static void ValidateEventName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Contains('\0'))
        {
            throw new ArgumentException("An event name cannot contain a null character.", nameof(name));
        }
    }

    private sealed class EventSubscription<T>(string name, EventHandler<T> handler) : IDisposable
    {
        private static readonly Lock Gate = new();
        private static readonly Dictionary<string, ImmutableArray<EventSubscription<T>>> Subscribers = new(StringComparer.Ordinal);
        private AssemblyLoadContext? _owner;

        public EventHandler<T> Handler { get; } = handler;

        public static EventSubscription<T> Add(string name, EventHandler<T> handler)
        {
            var subscription = new EventSubscription<T>(name, handler);
            var owner = AssemblyLoadContext.GetLoadContext(handler.Method.Module.Assembly);
            lock (Gate)
            {
                if (owner is { IsCollectible: true })
                {
                    subscription._owner = owner;
                    owner.Unloading += subscription.OnUnloading;
                }
                var current = Subscribers.GetValueOrDefault(name, []);
                Subscribers[name] = current.Add(subscription);
            }
            return subscription;
        }

        public static ImmutableArray<EventSubscription<T>> Snapshot(string name)
        {
            lock (Gate)
            {
                return Subscribers.GetValueOrDefault(name, []);
            }
        }

        private void OnUnloading(AssemblyLoadContext context)
        {
            Dispose();
        }

        public void Dispose()
        {
            lock (Gate)
            {
                if (_owner is not null)
                {
                    _owner.Unloading -= OnUnloading;
                    _owner = null;
                }
                if (!Subscribers.TryGetValue(name, out var current))
                {
                    return;
                }
                var remaining = current.Remove(this);
                if (remaining.IsEmpty)
                {
                    Subscribers.Remove(name);
                }
                else
                {
                    Subscribers[name] = remaining;
                }
            }
        }
    }
}
