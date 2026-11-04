namespace F4Forge.Net.Input;

/// <summary>Copied state of one XInput-compatible controller.</summary>
/// <param name="Connected">Whether the controller is available.</param>
/// <param name="Packet">XInput change counter.</param>
/// <param name="Buttons">XInput button mask.</param>
/// <param name="LeftX">Signed left-stick horizontal position.</param>
/// <param name="LeftY">Signed left-stick vertical position.</param>
public sealed record ControllerSnapshot(bool Connected, uint Packet, int Buttons, short LeftX, short LeftY);
