using F4Forge.Net.Operations;

namespace F4Forge.Net.UI.Queries;

/// <summary>Reads whether the Escape pause menu is on the engine menu stack. Requires the game thread.</summary>
public sealed record IsPauseMenuOpen : IQuery<bool>
{
    /// <inheritdoc />
    public string MethodName => "ui.is_pause_menu_open";

    /// <inheritdoc />
    public object? Arguments => null;
}
