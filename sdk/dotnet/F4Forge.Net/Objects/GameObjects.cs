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

    /// <summary>Resolves any runtime form ID; zero and missing forms return null.</summary>
    public Form? Get(uint formId)
    {
        return Wrap(_messages.Call<FormSnapshot?>("forms.describe", new { FormId = formId }));
    }

    /// <summary>Resolves a form and checks its typed view.</summary>
    public T? Get<T>(uint formId) where T : Form, IFormView<T>
    {
        return Get(formId)?.As<T>();
    }

    /// <summary>Resolves a plugin-local ID, including light plugins, independent of load-order index.</summary>
    public Form? Resolve(string plugin, uint localId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plugin);
        return Wrap(_messages.Call<FormSnapshot?>("forms.resolve", new { Plugin = plugin, LocalId = localId }));
    }

    /// <summary>Looks up an editor ID retained in the game's runtime index; unavailable IDs return null.</summary>
    public Form? Find(string editorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorId);
        return Wrap(_messages.Call<FormSnapshot?>("forms.resolve", new { EditorId = editorId }));
    }

    /// <summary>Resolves the player if present.</summary>
    public Actor? GetPlayer()
    {
        return Wrap(_messages.Call<FormSnapshot?>("forms.player", null))?.As<Actor>();
    }

    private Form? Wrap(FormSnapshot? snapshot)
    {
        return snapshot is null ? null : new Form(_messages, snapshot);
    }
}
