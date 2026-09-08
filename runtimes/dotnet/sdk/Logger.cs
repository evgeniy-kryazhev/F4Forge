namespace F4Forge.DotNet.Sdk;

public static class Logger
{
    internal static Action<F4ForgeLogLevel, string>? Sink { get; set; }

    public static void Trace(string message) => Write(F4ForgeLogLevel.Trace, message);
    public static void Debug(string message) => Write(F4ForgeLogLevel.Debug, message);
    public static void Info(string message) => Write(F4ForgeLogLevel.Info, message);
    public static void Warning(string message) => Write(F4ForgeLogLevel.Warning, message);
    public static void Error(string message) => Write(F4ForgeLogLevel.Error, message);
    public static void Critical(string message) => Write(F4ForgeLogLevel.Critical, message);

    private static void Write(F4ForgeLogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            Sink?.Invoke(level, message);
        }
        catch
        {
        }
    }
}
