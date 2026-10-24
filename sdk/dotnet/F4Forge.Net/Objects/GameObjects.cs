using F4Forge.Net.Messaging;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Objects;

/// <summary>Generic form resolution. Engine operations are synchronous and require the game thread.</summary>
public sealed partial class GameObjects
{
    private readonly IMessageChannel _messages;

    internal GameObjects(IMessageChannel messages)
    {
        _messages = messages;
    }

    /// <summary>Executes a typed lookup through the mod's shared message channel.</summary>
    /// <typeparam name="TForm">The form type inferred from the query contract.</typeparam>
    /// <param name="query">Module-owned method name and wire arguments.</param>
    /// <returns>A scoped form of the requested type, or null if the requested form does not exist.</returns>
    public TForm? Query<TForm>(IFormQuery<TForm> query) where TForm : Form, IFormType<TForm>
    {
        var snapshot = _messages.Query(query);
        if (snapshot is null)
        {
            return null;
        }

        return new Form(_messages, snapshot).As<TForm>();
    }
}
