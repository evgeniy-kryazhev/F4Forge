namespace F4Forge.Net.Messaging;

/// <summary>A synchronous, process-local channel shared by all SDKs.</summary>
public interface IMessageChannel
{
    /// <summary>Invokes a named method and deserializes its response.</summary>
    /// <param name="method">Unique method name, such as mymod.get_title.</param>
    /// <param name="arguments">Arguments supported by the shared serializer.</param>
    /// <returns>The response value.</returns>
    TResult Call<TResult>(string method, object? arguments);

    /// <summary>Registers a handler until the returned registration is disposed.</summary>
    /// <param name="method">Unique method name.</param>
    /// <param name="handler">Synchronous handler; never wait on another thread from here.</param>
    /// <returns>An owned registration that must be disposed before unloading the mod.</returns>
    IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler);
}
