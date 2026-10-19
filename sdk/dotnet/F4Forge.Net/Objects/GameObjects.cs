using F4Forge.Net.Messaging;

namespace F4Forge.Net.Objects;

/// <summary>Generic form resolution. Engine operations are synchronous and require the game thread.</summary>
public sealed class GameObjects
{
    private readonly IMessageChannel _messages;

    internal GameObjects(IMessageChannel messages)
    {
        _messages = messages;
    }

    /// <summary>Executes a typed lookup through the mod's shared message channel.</summary>
    /// <typeparam name="TForm">The view inferred from the query contract.</typeparam>
    /// <param name="query">Module-owned method name and wire arguments.</param>
    /// <returns>A scoped typed view, or null if the requested form does not exist.</returns>
    public TForm? Query<TForm>(IFormQuery<TForm> query) where TForm : Form, IFormView<TForm>
    {
        ArgumentNullException.ThrowIfNull(query);
        var snapshot = _messages.Call<FormSnapshot?>(query.MethodName, query.Arguments);
        if (snapshot is null)
        {
            return null;
        }

        return new Form(_messages, snapshot).As<TForm>();
    }
}
