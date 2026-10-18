namespace F4Forge.Net.Objects;

/// <summary>A copied description of any form, valid for identifying one game session.</summary>
/// <param name="FormId">Runtime form ID.</param>
/// <param name="Session">Host session generation.</param>
/// <param name="Type">Engine record signature, such as WEAP or ACHR.</param>
/// <param name="Name">Display name, possibly empty.</param>
/// <param name="EditorId">Editor ID if retained by the game.</param>
/// <param name="IsReference">Whether the form is a world reference.</param>
/// <param name="IsActor">Whether the form is an actor.</param>
/// <param name="IsActorValue">Whether the form defines an actor value.</param>
public sealed record FormSnapshot(uint FormId, ulong Session, string Type, string Name, string EditorId,
    bool IsReference, bool IsActor, bool IsActorValue);

/// <summary>Copied position in game world units.</summary>
/// <param name="X">X coordinate.</param>
/// <param name="Y">Y coordinate.</param>
/// <param name="Z">Z coordinate.</param>
public readonly record struct Position(float X, float Y, float Z);

/// <summary>One copied world-reference snapshot; absent forms use ID zero.</summary>
/// <param name="BaseFormId">Base definition.</param>
/// <param name="CellFormId">Parent cell.</param>
/// <param name="LocationFormId">Current named location.</param>
/// <param name="Position">World position.</param>
public sealed record ReferenceSnapshot(uint BaseFormId, uint CellFormId, uint LocationFormId, Position Position);

/// <summary>Values readable through the same generic actor-value operation.</summary>
public enum ActorValueKind
{
    /// <summary>Current value including modifiers.</summary>
    Current,
    /// <summary>Base value.</summary>
    Base,
    /// <summary>Permanent value.</summary>
    Permanent
}
