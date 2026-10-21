using F4Forge.Net.Messaging;

namespace F4Forge.Net.Singletons;

/// <summary>A typed, borrowed proxy for a CommonLibF4 singleton.</summary>
/// <typeparam name="TSelf">The generated proxy type.</typeparam>
public interface IGameSingleton<TSelf> where TSelf : class, IGameSingleton<TSelf>
{
    /// <summary>Creates a proxy using the owning mod's shared message channel.</summary>
    /// <param name="messages">The owning mod's channel.</param>
    /// <returns>A proxy that never owns or frees the engine object.</returns>
    static abstract TSelf Create(IMessageChannel messages);
}
