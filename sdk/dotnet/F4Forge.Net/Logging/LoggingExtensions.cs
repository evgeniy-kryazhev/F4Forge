using F4Forge.Net.Logging.Commands;
using F4Forge.Net.Mods;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Logging;

/// <summary>Writes mod messages to the native F4SE plugin logger.</summary>
public static class LoggingExtensions
{
    /// <summary>Writes an informational message, prefixed with the mod directory name.</summary>
    public static void LogInfo(this ModContext context, string message)
    {
        Write(context, "info", message);
    }

    /// <summary>Writes a warning, prefixed with the mod directory name.</summary>
    public static void LogWarning(this ModContext context, string message)
    {
        Write(context, "warning", message);
    }

    /// <summary>Writes an error, prefixed with the mod directory name.</summary>
    public static void LogError(this ModContext context, string message)
    {
        Write(context, "error", message);
    }

    private static void Write(ModContext context, string level, string message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        var source = Path.GetFileName(Path.TrimEndingDirectorySeparator(context.Directory));
        context.Messages.Execute(new WriteLog(level, source, message));
    }
}
