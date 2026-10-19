using F4Forge.Net.Events;

namespace F4Forge.Net.UI.Events;

/// <summary>A menu opened or closed.</summary>
/// <param name="MenuName">The engine menu name.</param>
/// <param name="Opening">True for opening; false for closing.</param>
public sealed record MenuChanged(string MenuName, bool Opening) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.menu.changed";
}
