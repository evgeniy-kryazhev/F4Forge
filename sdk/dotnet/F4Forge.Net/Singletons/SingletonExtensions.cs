using System.Runtime.CompilerServices;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Singletons;

/// <summary>Generic access to generated CommonLibF4 singleton proxies.</summary>
public static class SingletonExtensions
{
    /// <summary>Gets this mod context's cached proxy. Engine access occurs when a member is invoked.</summary>
    /// <typeparam name="T">A generated singleton proxy.</typeparam>
    /// <param name="context">The mod owning the proxy's channel.</param>
    /// <returns>A typed proxy. Getting it does not require the engine singleton to exist yet.</returns>
    /// <remarks>Members, including IsAvailable, require the game thread. Native pointers are never cached.</remarks>
    public static T GetSingleton<T>(this ModContext context) where T : class, IGameSingleton<T>
    {
        ArgumentNullException.ThrowIfNull(context);
        return ProxyCache<T>.Instances.GetValue(context, static owner => T.Create(owner.Messages));
    }

    private static class ProxyCache<T> where T : class, IGameSingleton<T>
    {
        internal static readonly ConditionalWeakTable<ModContext, T> Instances = new();
    }
}
