using F4Forge.Net.Messaging;

namespace F4Forge.Net.Operations;

/// <summary>Executes typed operations without per-feature wrappers or dispatch tables.</summary>
public static class OperationExtensions
{
    /// <summary>Reads copied data synchronously on the calling thread.</summary>
    /// <typeparam name="TResult">The response inferred from the query.</typeparam>
    /// <param name="messages">The mod-owned message channel.</param>
    /// <param name="query">The query and its wire arguments.</param>
    /// <returns>The deserialized response.</returns>
    public static TResult Query<TResult>(this IMessageChannel messages, IQuery<TResult> query)
    {
        return Invoke<TResult>(messages, query);
    }

    /// <summary>Changes state synchronously on the calling thread.</summary>
    /// <typeparam name="TResult">The response inferred from the command.</typeparam>
    /// <param name="messages">The mod-owned message channel.</param>
    /// <param name="command">The command and its wire arguments.</param>
    /// <returns>The native result; false and other acknowledgements are preserved.</returns>
    public static TResult Execute<TResult>(this IMessageChannel messages, ICommand<TResult> command)
    {
        return Invoke<TResult>(messages, command);
    }

    private static TResult Invoke<TResult>(IMessageChannel messages, IOperation operation)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(operation);
        operation.Validate(messages);
        return messages.Call<TResult>(operation.MethodName, operation.Arguments);
    }
}
