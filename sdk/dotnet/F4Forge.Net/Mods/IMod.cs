namespace F4Forge.Net.Mods;

/// <summary>Entry point for a managed Fallout 4 plugin.</summary>
public interface IMod
{
    /// <summary>Initializes the plugin when F4SE loads F4Forge.</summary>
    /// <param name="directory">Absolute path to the folder containing the plugin DLL.</param>
    void Initialize(string directory);
}
