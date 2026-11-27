namespace F4Forge.Input;

using F4Forge.Bridge;

/// <summary>Input notifications delivered by Fallout's MenuControls on the notifying thread.</summary>
public static class InputEvents
{
    private static readonly Bridge.NamedEvent<Events.ButtonEvent> Button = new(Events.ButtonEvent.Name);
    private static readonly Bridge.NamedEvent<Events.CharacterEvent> Character = new(Events.CharacterEvent.Name);
    private static readonly Bridge.NamedEvent<Events.MouseMoveEvent> MouseMove = new(Events.MouseMoveEvent.Name);
    private static readonly Bridge.NamedEvent<Events.CursorMoveEvent> CursorMove = new(Events.CursorMoveEvent.Name);

    /// <summary>Button state including device, button code, analog value and time held.</summary>
    public static event EventHandler<Events.ButtonEvent> OnButton
    {
        add => Button.Add(value);
        remove => Button.Remove(value);
    }

    /// <summary>Character input with the engine's character code.</summary>
    public static event EventHandler<Events.CharacterEvent> OnCharacter
    {
        add => Character.Add(value);
        remove => Character.Remove(value);
    }

    /// <summary>Relative mouse movement.</summary>
    public static event EventHandler<Events.MouseMoveEvent> OnMouseMove
    {
        add => MouseMove.Add(value);
        remove => MouseMove.Remove(value);
    }

    /// <summary>Movement of the game's cursor with its reported position.</summary>
    public static event EventHandler<Events.CursorMoveEvent> OnCursorMove
    {
        add => CursorMove.Add(value);
        remove => CursorMove.Remove(value);
    }
}
