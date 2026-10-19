namespace F4Forge.Net.Events;

/// <summary>A typed notification whose name is shared with its native publisher.</summary>
public interface IGameEvent
{
    /// <summary>The stable name used by the shared event channel.</summary>
    static abstract string EventName { get; }
}
