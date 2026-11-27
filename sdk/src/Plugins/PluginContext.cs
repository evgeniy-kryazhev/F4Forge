namespace F4Forge;

/// <summary>Information supplied by the host when starting a plugin.</summary>
public class PluginContext
{
    /// <summary>The absolute path to the directory containing the plugin assembly.</summary>
    public string Directory { get; internal init; } = string.Empty;
}
