using System.Runtime.CompilerServices;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Singletons;

/// <summary>Generic access to engine singleton proxies and context-owned services.</summary>
public static class SingletonExtensions
{
    /// <summary>Gets this mod context's cached proxy. Engine access occurs when a member is invoked.</summary>
    /// <typeparam name="T">A supported singleton proxy or service.</typeparam>
    /// <param name="context">The mod owning the proxy's channel.</param>
    /// <returns>A typed proxy. Getting it does not require the engine singleton to exist yet.</returns>
    /// <remarks>Engine proxy members require the game thread. Scheduler services can be called from other threads. Native pointers are never cached.</remarks>
    public static T GetSingleton<T>(this ModContext context) where T : class, IGameSingleton<T>
    {
        ArgumentNullException.ThrowIfNull(context);
        // ConditionalWeakTable may invoke its factory concurrently. Only the stored
        // Lazy may construct a service, because construction can register cleanup.
        var singleton = ProxyCache<T>.Instances.GetValue(context, static owner =>
        {
            return new Lazy<T>(() => T.Create(owner), LazyThreadSafetyMode.ExecutionAndPublication);
        });
        return singleton.Value;
    }

    private static class ProxyCache<T> where T : class, IGameSingleton<T>
    {
        internal static readonly ConditionalWeakTable<ModContext, Lazy<T>> Instances = new();
    }
}
