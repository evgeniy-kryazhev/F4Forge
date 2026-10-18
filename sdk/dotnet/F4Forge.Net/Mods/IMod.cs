namespace F4Forge.Net.Mods;

/// <summary>A mod entry point discovered inside its isolated assembly context.</summary>
public interface IMod : IDisposable
{
    /// <summary>Starts the mod and registers its message handlers.</summary>
    /// <param name="context">Mod-owned services; registrations are cleaned up automatically.</param>
    void Start(ModContext context);
}
