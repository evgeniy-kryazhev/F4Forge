namespace F4Forge.Net.Mods;

/// <summary>A mod entry point discovered inside its isolated assembly context.</summary>
public interface IMod : IAsyncDisposable
{
    /// <summary>Starts the mod and registers its message handlers.</summary>
    /// <param name="context">Mod-owned services; registrations are cleaned up automatically.</param>
    /// <param name="cancellationToken">Cancelled when the host stops the mod.</param>
    Task StartAsync(ModContext context, CancellationToken cancellationToken);
}
