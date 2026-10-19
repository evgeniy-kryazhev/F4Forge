using F4Forge.Net.Messaging;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Operations;

namespace F4Forge.Net.Actors.Operations;

/// <summary>Shared ownership and session checks for actor-value reads and writes.</summary>
/// <param name="Actor">The target actor.</param>
/// <param name="ActorValue">The actor-value definition in the same scope and session.</param>
public abstract record ActorValueOperation(Actor Actor, ActorValue ActorValue) : FormOperation(Actor)
{
    /// <inheritdoc />
    public override void Validate(IMessageChannel messages)
    {
        base.Validate(messages);
        ArgumentNullException.ThrowIfNull(Actor);
        ArgumentNullException.ThrowIfNull(ActorValue);
        // Positional records can be copied with changed properties. Never allow
        // the wire target to diverge from the identity validated by the base type.
        if (!ReferenceEquals(Actor, Form) || Actor.Session != ActorValue.Session ||
            !ReferenceEquals(ActorValue.Messages, messages))
        {
            throw new ArgumentException("Actor and value must belong to the same game session and mod scope.");
        }
    }
}
