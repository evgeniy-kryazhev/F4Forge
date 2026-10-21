using F4Forge.Net.Events;

namespace F4Forge.Net.Input.Events;

/// <summary>A keyboard key, mouse button, or gamepad button was pressed.</summary>
/// <param name="Device">The input device.</param>
/// <param name="KeyCode">The engine code: keyboard virtual-key 0–255, mouse 256–263, gamepad 266–281.</param>
/// <param name="UserEvent">The engine action name, or an empty string when unassigned.</param>
/// <param name="HeldSeconds">Zero for a new press.</param>
public sealed record KeyDown(InputDevice Device, KeyCode KeyCode, string UserEvent, float HeldSeconds) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.input.key.down";
}
