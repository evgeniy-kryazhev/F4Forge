using F4Forge.Net.Actors.Operations;
using F4Forge.Net.Messaging;
using F4Forge.Net.Objects;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Actors.Commands;

/// <summary>Sets an actor's base value, preserving damage and temporary modifiers.</summary>
/// <param name="Actor">The target actor.</param>
/// <param name="ActorValue">The statistic definition in the same scope and session.</param>
/// <param name="Value">The finite new base value.</param>
public sealed record SetActorBaseValue(Actor Actor, ActorValue ActorValue, float Value)
    : ActorValueOperation(Actor, ActorValue), ICommand<bool>
{
    /// <inheritdoc />
    public override string MethodName => "forms.set_base_value";

    /// <inheritdoc />
    public override object Arguments => new
    {
        Actor.FormId,
        Actor.Session,
        ActorValueFormId = ActorValue.FormId,
        ActorValueSession = ActorValue.Session,
        Value
    };

    /// <inheritdoc />
    public override void Validate(IMessageChannel messages)
    {
        base.Validate(messages);
        if (!float.IsFinite(Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Value));
        }
    }
}
