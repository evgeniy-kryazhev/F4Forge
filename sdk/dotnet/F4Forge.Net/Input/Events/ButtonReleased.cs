using F4Forge.Net.Events;

namespace F4Forge.Net.Input.Events;

/// <summary>A button was released, including while menus are open.</summary>
/// <param name="Device">The input device.</param>
/// <param name="KeyCode">The F4SE key code: keyboard 0–255, mouse 256–263, gamepad 266–281.</param>
/// <param name="UserEvent">The engine action name, or an empty string when unassigned.</param>
/// <param name="HeldSeconds">The hold duration reported by the engine, in seconds.</param>
public sealed record ButtonReleased(InputDevice Device, KeyCode KeyCode, string UserEvent, float HeldSeconds) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.input.button.released";
}
