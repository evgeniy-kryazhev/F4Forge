namespace F4Forge;

/// <summary>Information supplied by the host when starting a plugin.</summary>
public class PluginContext
{
    /// <summary>Creates a standalone context that logs to standard error.</summary>
    public PluginContext() : this(string.Empty, new PluginLogger("Plugin", Console.Error.WriteLine))
    {
    }

    internal PluginContext(string directory, PluginLogger log)
    {
        Directory = directory;
        Log = log;
    }

    /// <summary>The absolute path to the directory containing the plugin assembly.</summary>
    public string Directory { get; internal init; }

    /// <summary>The logger for this plugin. It can also be used from event handlers and worker threads.</summary>
    public PluginLogger Log { get; }
}
