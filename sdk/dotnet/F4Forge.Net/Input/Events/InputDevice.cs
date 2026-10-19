namespace F4Forge.Net.Input.Events;

/// <summary>The device that produced a button notification.</summary>
public enum InputDevice
{
    /// <summary>A physical keyboard.</summary>
    Keyboard = 0,

    /// <summary>A mouse button, excluding its wheel.</summary>
    Mouse = 1,

    /// <summary>A gamepad button or trigger.</summary>
    Gamepad = 2
}
