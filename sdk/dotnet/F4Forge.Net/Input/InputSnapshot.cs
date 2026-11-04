namespace F4Forge.Net.Input;

/// <summary>A copied snapshot of pause state and Shift/Control/Alt modifier bits.</summary>
/// <param name="Paused">Whether a menu pauses gameplay or game data is unavailable.</param>
/// <param name="Modifiers">Shift=1, Control=2, Alt=4.</param>
public sealed record InputSnapshot(bool Paused, int Modifiers);
