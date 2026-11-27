using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace F4Forge.Hosting;

internal static unsafe class Bootstrap
{
    // Keep initialized plugins alive for the game session.
    private static PluginLoader? _loader;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Start(char* pluginDirectory)
    {
        if (pluginDirectory is null)
        {
            return 1;
        }

        try
        {
            var directory = new string(pluginDirectory);
            var logPath = Path.Combine(directory, "F4Forge", "Net.log");
            var loader = new PluginLoader(message => Log(logPath, message));
            if (Interlocked.CompareExchange(ref _loader, loader, null) is not null)
            {
                return 1;
            }

            try
            {
                Bridge.ConnectNativeEvents(message => Log(logPath, message));
                var managedPluginDirectory = Path.GetFullPath(Path.Combine(directory, "..", "..", "F4ForgeNet"));
                loader.LoadDirectory(managedPluginDirectory);
                return 0;
            }
            catch (Exception exception)
            {
                Log(logPath, exception.ToString());
                _loader = null;
                return 1;
            }
        }
        catch
        {
            // Exceptions must stay on the managed side of the F4SE entry point.
            return 1;
        }
    }

    private static void Log(string logPath, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // A log write failure must not interrupt discovery.
        }
        catch (UnauthorizedAccessException)
        {
            // The game directory may be read-only.
        }
    }
}
