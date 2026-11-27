namespace F4Forge;

/// <summary>Gameplay notifications from CommonLibF4.</summary>
public static class GameEvents
{
    private static readonly Bridge.NamedEvent<Events.TESLoadGameEvent> GameLoaded = new(Events.TESLoadGameEvent.Name);
    private static readonly Bridge.NamedEvent<Events.DoBeforeNewOrLoadCompletedEvent> BeforeNewOrLoadCompleted =
        new(Events.DoBeforeNewOrLoadCompletedEvent.Name);
    private static readonly Bridge.NamedEvent<Events.TESCellFullyLoadedEvent> CellFullyLoaded = new(Events.TESCellFullyLoadedEvent.Name);
    private static readonly Bridge.NamedEvent<Events.TESObjectLoadedEvent> ObjectLoadChanged = new(Events.TESObjectLoadedEvent.Name);

    /// <summary>Raised on the notifying thread when the game publishes TESLoadGameEvent.</summary>
    public static event EventHandler<Events.TESLoadGameEvent> OnGameLoaded
    {
        add => GameLoaded.Add(value);
        remove => GameLoaded.Remove(value);
    }

    /// <summary>Raised when the game publishes its notification before new-game or load completion.</summary>
    public static event EventHandler<Events.DoBeforeNewOrLoadCompletedEvent> OnBeforeNewOrLoadCompleted
    {
        add => BeforeNewOrLoadCompleted.Add(value);
        remove => BeforeNewOrLoadCompleted.Remove(value);
    }

    /// <summary>Raised when a cell is fully loaded. Cell is its borrowed native address.</summary>
    public static event EventHandler<Events.TESCellFullyLoadedEvent> OnCellFullyLoaded
    {
        add => CellFullyLoaded.Add(value);
        remove => CellFullyLoaded.Remove(value);
    }

    /// <summary>Raised when an object loads or unloads. Loaded is true on load and false on unload.</summary>
    public static event EventHandler<Events.TESObjectLoadedEvent> OnObjectLoadChanged
    {
        add => ObjectLoadChanged.Add(value);
        remove => ObjectLoadChanged.Remove(value);
    }
}
