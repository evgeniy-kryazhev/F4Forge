using F4Forge.Net.Mods;

namespace F4Forge.Net.Singletons;

/// <summary>A typed engine singleton proxy or context-owned service.</summary>
/// <typeparam name="TSelf">The singleton proxy or service type.</typeparam>
public interface IGameSingleton<TSelf> where TSelf : class, IGameSingleton<TSelf>
{
    /// <summary>Creates a singleton proxy or service for the owning mod context.</summary>
    /// <param name="context">The owning mod context.</param>
    /// <returns>A proxy or service that does not own engine objects.</returns>
    static abstract TSelf Create(ModContext context);
}
