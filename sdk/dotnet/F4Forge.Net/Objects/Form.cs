using F4Forge.Net.Messaging;

namespace F4Forge.Net.Objects;

/// <summary>A scoped form identity. Every operation resolves it again in C++; it owns no native object.</summary>
public class Form : IFormType<Form>
{
    /// <summary>Accepts every copied form description.</summary>
    /// <param name="snapshot">Copied native form data.</param>
    /// <returns>True because Form accepts every form type.</returns>
    public static bool Matches(FormSnapshot snapshot) => true;

    /// <summary>Retains a generic form identity without allocating another wrapper.</summary>
    /// <param name="form">Resolved form in the current mod scope.</param>
    /// <returns>The supplied form.</returns>
    public static Form Create(Form form) => form;

    internal Form(IMessageChannel messages, FormSnapshot snapshot)
    {
        Messages = messages;
        Snapshot = snapshot;
    }

    /// <summary>Allows external form types to retain the original identity and mod scope.</summary>
    protected Form(Form source) : this(source.Messages, source.Snapshot)
    {
    }

    /// <summary>The mod's scoped message channel for additive feature extensions.</summary>
    public IMessageChannel Messages { get; }

    /// <summary>The copied description at resolution time; query DescribeForm for current data.</summary>
    public FormSnapshot Snapshot { get; }

    /// <summary>Runtime form ID; it is not a persistent cross-session identifier.</summary>
    public uint FormId => Snapshot.FormId;

    /// <summary>Session generation that native operations must validate.</summary>
    public ulong Session => Snapshot.Session;

    /// <summary>Tests a form type using the copied description, without accessing game memory.</summary>
    /// <typeparam name="T">The form type to check.</typeparam>
    /// <returns>Whether the copied description matches the requested type.</returns>
    public bool Is<T>() where T : Form, IFormType<T>
    {
        return T.Matches(Snapshot);
    }

    /// <summary>Creates a typed form wrapper, or throws if the copied type is incompatible.</summary>
    /// <typeparam name="T">The form type to create.</typeparam>
    /// <returns>A wrapper retaining this form's identity and mod scope.</returns>
    public T As<T>() where T : Form, IFormType<T>
    {
        if (!T.Matches(Snapshot))
        {
            throw new InvalidCastException($"Form {FormId:X8} ({Snapshot.Type}) cannot be converted to {typeof(T).Name}.");
        }
        return T.Create(this);
    }
}
