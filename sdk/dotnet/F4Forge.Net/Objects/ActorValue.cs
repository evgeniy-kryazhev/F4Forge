namespace F4Forge.Net.Objects;

/// <summary>An actor-value definition, resolved as a form rather than a hardcoded SDK enumeration.</summary>
public sealed class ActorValue : Form, IFormType<ActorValue>
{
    private ActorValue(Form source) : base(source)
    {
        if (!source.Snapshot.IsActorValue)
        {
            throw new InvalidCastException("The supplied form is not an actor-value definition.");
        }
    }

    /// <inheritdoc />
    public new static bool Matches(FormSnapshot snapshot) => snapshot.IsActorValue;

    /// <inheritdoc />
    public new static ActorValue Create(Form form) => new(form);
}
