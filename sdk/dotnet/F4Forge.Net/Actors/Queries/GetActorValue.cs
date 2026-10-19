using F4Forge.Net.Actors.Operations;
using F4Forge.Net.Objects;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Actors.Queries;

/// <summary>Reads a built-in or mod-defined actor value.</summary>
/// <param name="Actor">The target actor.</param>
/// <param name="ActorValue">The statistic definition in the same scope and session.</param>
/// <param name="Kind">Current, base or permanent value.</param>
public sealed record GetActorValue(Actor Actor, ActorValue ActorValue, ActorValueKind Kind = ActorValueKind.Current)
    : ActorValueOperation(Actor, ActorValue), IQuery<float>
{
    /// <inheritdoc />
    public override string MethodName => "forms.actor_value";

    /// <inheritdoc />
    public override object Arguments
    {
        get
        {
            var wireKind = Kind switch
            {
                ActorValueKind.Current => "current",
                ActorValueKind.Base => "base",
                ActorValueKind.Permanent => "permanent",
                _ => throw new ArgumentOutOfRangeException(nameof(Kind))
            };
            return new
            {
                Actor.FormId,
                Actor.Session,
                ActorValueFormId = ActorValue.FormId,
                ActorValueSession = ActorValue.Session,
                Kind = wireKind
            };
        }
    }
}
