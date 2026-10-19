namespace F4Forge.Net.Objects;

/// <summary>An actor identity. Actor values cover both built-in and mod-defined statistics.</summary>
public sealed class Actor : Reference, IFormType<Actor>
{
    private Actor(Form source) : base(source)
    {
        if (!source.Snapshot.IsActor)
        {
            throw new InvalidCastException("The supplied form is not an actor.");
        }
    }

    /// <inheritdoc />
    public new static bool Matches(FormSnapshot snapshot) => snapshot.IsActor;

    /// <inheritdoc />
    public new static Actor Create(Form form) => new(form);
}
