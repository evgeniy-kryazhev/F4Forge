namespace F4Forge.Actors;

using F4Forge.Bridge;

/// <summary>A borrowed reference to a live Fallout 4 actor. Use it only while the game owns the actor.</summary>
public sealed record Actor
{
    private Actor(nint handle)
    {
        Handle = handle;
    }

    /// <summary>The borrowed RE::Actor address for low-level calls.</summary>
    public nint Handle { get; }

    /// <summary>The form ID of this actor reference.</summary>
    public uint FormId => Bindings.TESForm.GetFormID(Handle);

    /// <summary>Whether the actor is currently in combat.</summary>
    public bool IsInCombat => Bindings.Actor.IsInCombat(Handle);

    /// <summary>Whether the actor is currently sneaking.</summary>
    public bool IsSneaking => Bindings.Actor.IsSneaking(Handle);

    /// <summary>Wraps a live RE::Actor address from the bridge. A null address returns null.</summary>
    public static Actor? FromHandle(nint handle)
    {
        return handle == 0 ? null : new Actor(handle);
    }

    /// <summary>Formats the borrowed address without querying the game.</summary>
    public override string ToString() => $"Actor(0x{Handle:X})";
}
