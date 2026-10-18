namespace F4Forge.Net.Objects;

/// <summary>Additive actor operations without editing the actor type or transport.</summary>
public static class ActorValueExtensions
{
    /// <summary>Reads any actor-value definition from the same game session and mod scope.</summary>
    public static float GetValue(this Actor actor, ActorValue value, ActorValueKind kind = ActorValueKind.Current)
    {
        Validate(actor, value);
        var wireKind = kind switch
        {
            ActorValueKind.Current => "current",
            ActorValueKind.Base => "base",
            ActorValueKind.Permanent => "permanent",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return actor.Messages.Call<float>("forms.actor_value", new
        {
            actor.FormId,
            actor.Session,
            ActorValueFormId = value.FormId,
            ActorValueSession = value.Session,
            Kind = wireKind
        });
    }

    /// <summary>Sets the base value. It does not clear damage or temporary modifiers.</summary>
    public static void SetBaseValue(this Actor actor, ActorValue value, float amount)
    {
        Validate(actor, value);
        if (!float.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
        actor.Messages.Call<bool>("forms.set_base_value", new
        {
            actor.FormId,
            actor.Session,
            ActorValueFormId = value.FormId,
            ActorValueSession = value.Session,
            Value = amount
        });
    }

    private static void Validate(Actor actor, ActorValue value)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(value);
        if (actor.Session != value.Session || !ReferenceEquals(actor.Messages, value.Messages))
        {
            throw new ArgumentException("Actor and value must belong to the same game session and mod scope.", nameof(value));
        }
    }
}
