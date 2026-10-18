namespace F4Forge.Net.Objects;

/// <summary>An actor view. Universal actor values cover both built-in and mod-defined statistics.</summary>
public sealed class Actor : Reference, IFormView<Actor>
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

/// <summary>An actor-value definition, resolved as a form rather than a hardcoded SDK enumeration.</summary>
public sealed class ActorValue : Form, IFormView<ActorValue>
{
    private ActorValue(Form source) : base(source)
    {
        if (!source.Snapshot.IsActorValue)
        {
            throw new InvalidCastException("The supplied form is not an actor-value definition.");
        }
    }

    /// <inheritdoc />
    public static bool Matches(FormSnapshot snapshot) => snapshot.IsActorValue;

    /// <inheritdoc />
    public static ActorValue Create(Form form) => new(form);
}
