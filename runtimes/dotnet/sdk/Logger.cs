namespace F4Forge.DotNet.Sdk;

public static class Logger
{
    internal static Action<string>? Sink { get; set; }

    public static void Trace(string message) => Write(message);
    public static void Debug(string message) => Write(message);
    public static void Info(string message) => Write(message);
    public static void Warning(string message) => Write(message);
    public static void Error(string message) => Write(message);

    private static void Write(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            Sink?.Invoke(message);
        }
        catch
        {
        }
    }
}
