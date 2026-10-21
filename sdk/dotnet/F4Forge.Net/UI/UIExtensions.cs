using F4Forge.Net.Mods;
using F4Forge.Net.Operations;
using F4Forge.Net.UI.Queries;

namespace F4Forge.Net.UI;

/// <summary>Queries the current engine UI state through the shared message channel.</summary>
public static class UIExtensions
{
    /// <summary>Reads whether the Escape pause menu is open. Call on the game thread or through GameThread.RunAsync.</summary>
    /// <param name="context">The mod context owning the message channel.</param>
    /// <returns>True when PauseMenu is on the menu stack; false when closed or the engine UI is unavailable.</returns>
    public static bool IsPauseMenuOpen(this ModContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Messages.Query(new IsPauseMenuOpen());
    }
}
