using F4Forge.Net.Messaging;

namespace F4Forge.Net.Objects;

/// <summary>Extensible typed views require no central type switch or native exports.</summary>
/// <typeparam name="TSelf">The typed form view.</typeparam>
public interface IFormView<TSelf> where TSelf : Form
{
    /// <summary>Checks the copied native description.</summary>
    static abstract bool Matches(FormSnapshot snapshot);

    /// <summary>Constructs a view retaining the original scope and identity.</summary>
    static abstract TSelf Create(Form form);
}

/// <summary>A scoped form identity. Every operation resolves it again in C++; it owns no native object.</summary>
public class Form : IFormView<Form>
{
    /// <summary>Accepts every copied form description.</summary>
    /// <param name="snapshot">Copied native form data.</param>
    /// <returns>True because this view represents every form type.</returns>
    public static bool Matches(FormSnapshot snapshot) => true;

    /// <summary>Retains a generic form identity without allocating another view.</summary>
    /// <param name="form">Resolved form in the current mod scope.</param>
    /// <returns>The supplied form.</returns>
    public static Form Create(Form form) => form;

    internal Form(IMessageChannel messages, FormSnapshot snapshot)
    {
        Messages = messages;
        Snapshot = snapshot;
    }

    /// <summary>Allows external typed views to retain the original identity and mod scope.</summary>
    protected Form(Form source) : this(source.Messages, source.Snapshot)
    {
    }

    /// <summary>The mod's scoped message channel for additive feature extensions.</summary>
    public IMessageChannel Messages { get; }

    /// <summary>The copied description at resolution time; use Describe for current data.</summary>
    public FormSnapshot Snapshot { get; }

    /// <summary>Runtime form ID; it is not a persistent cross-session identifier.</summary>
    public uint FormId => Snapshot.FormId;

    /// <summary>Session generation that native operations must validate.</summary>
    public ulong Session => Snapshot.Session;

    /// <summary>Fetches a fresh description; returns null if the form no longer exists.</summary>
    public FormSnapshot? Describe()
    {
        return Messages.Call<FormSnapshot?>("forms.describe", new { FormId, Session });
    }

    /// <summary>Tests a typed view using the copied description, without accessing game memory.</summary>
    public bool Is<T>() where T : Form, IFormView<T>
    {
        return T.Matches(Snapshot);
    }

    /// <summary>Creates a typed view, or throws if the copied type is incompatible.</summary>
    public T As<T>() where T : Form, IFormView<T>
    {
        if (!T.Matches(Snapshot))
        {
            throw new InvalidCastException($"Form {FormId:X8} ({Snapshot.Type}) cannot be viewed as {typeof(T).Name}.");
        }
        return T.Create(this);
    }
}
