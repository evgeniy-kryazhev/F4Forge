namespace F4Forge.Plugins;

/// <summary>Entry point for a managed Fallout 4 plugin.</summary>
public interface IPlugin
{
    /// <summary>Starts the plugin with its host-provided context.</summary>
    void Start(PluginContext context);
}
