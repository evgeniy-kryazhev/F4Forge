namespace F4Forge;

/// <summary>Provides access to the current player character.</summary>
public static class Player
{
    /// <summary>Gets the current player as an actor, or null if the game has no player yet.</summary>
    public static Actor? GetSingleton()
    {
        // PlayerCharacter's primary base is Actor in CommonLibF4.
        return Actor.FromHandle(Bindings.PlayerCharacter.GetSingleton());
    }
}
