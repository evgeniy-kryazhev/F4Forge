using F4Forge.Net.Events;

namespace F4Forge.Net.Input.Events;

/// <summary>A keyboard key, mouse button, or gamepad button was released.</summary>
/// <param name="Device">The input device.</param>
/// <param name="KeyCode">The engine code: keyboard virtual-key 0–255, mouse 256–263, gamepad 266–281.</param>
/// <param name="UserEvent">The engine action name, or an empty string when unassigned.</param>
/// <param name="HeldSeconds">The hold duration reported by the engine, in seconds.</param>
public sealed record KeyUp(InputDevice Device, KeyCode KeyCode, string UserEvent, float HeldSeconds) : IGameEvent
{
    /// <inheritdoc />
    public static string EventName => "game.input.key.up";
}
