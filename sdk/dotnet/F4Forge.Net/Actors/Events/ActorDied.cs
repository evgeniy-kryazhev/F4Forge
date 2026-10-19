using F4Forge.Net.Events;

namespace F4Forge.Net.Actors.Events;

/// <summary>An actor completed dying; preliminary dying notifications are excluded.</summary>
/// <param name="ActorFormId">The dying actor reference ID.</param>
/// <param name="KillerFormId">The killer reference ID, or zero when absent.</param>
/// <param name="IsPlayer">Whether the dying actor is the player.</param>
public sealed record ActorDied(uint ActorFormId, uint KillerFormId, bool IsPlayer) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.actor.died";
}
