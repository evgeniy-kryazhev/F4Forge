using F4Forge.Net.Objects;

namespace F4Forge.Net.Player.Queries;

/// <summary>Resolves the current player as an actor, or null if absent.</summary>
public sealed record GetPlayer : IFormQuery<Actor>
{
    /// <inheritdoc />
    public string MethodName => "forms.player";

    /// <inheritdoc />
    public object? Arguments => null;
}
