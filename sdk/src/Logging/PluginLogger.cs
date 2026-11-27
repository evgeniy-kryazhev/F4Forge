namespace F4Forge.Logging;

/// <summary>Writes messages to the managed host log under the plugin's assembly name.</summary>
public sealed class PluginLogger
{
    private readonly string _source;
    private readonly Action<string> _write;

    internal PluginLogger(string source, Action<string> write)
    {
        _source = source;
        _write = write;
    }

    /// <summary>Writes a diagnostic message.</summary>
    public void Debug(string message) => Write("DEBUG", message);

    /// <summary>Writes an informational message.</summary>
    public void Info(string message) => Write("INFO", message);

    /// <summary>Writes a warning.</summary>
    public void Warn(string message) => Write("WARN", message);

    /// <summary>Writes an error and, when supplied, its exception details.</summary>
    public void Error(string message, Exception? exception = null)
    {
        if (exception is not null)
        {
            message = $"{message}{Environment.NewLine}{exception}";
        }
        Write("ERROR", message);
    }

    private void Write(string level, string message)
    {
        _write($"[{level}] [{_source}] {message}");
    }
}
