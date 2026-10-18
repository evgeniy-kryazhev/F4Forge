using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.Net.Interop;

namespace F4Forge.Net.Hosting;

internal static unsafe class Bootstrap
{
    private static ModLoader? _loader;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Arguments
    {
        public ChannelBinding Channel;
        public char* Directory;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Start(Arguments* arguments)
    {
        try
        {
            if (arguments is null || arguments->Directory is null || _loader is not null
                || arguments->Channel.Context == 0 || arguments->Channel.Call is null
                || arguments->Channel.Add is null || arguments->Channel.Remove is null)
            {
                return 1;
            }
            var directory = Path.Combine(new string(arguments->Directory), "F4Forge");
            System.IO.Directory.CreateDirectory(directory);
            var channel = new NativeChannel(arguments->Channel);
            _loader = new ModLoader(channel, channel, channel.Serializer, message => Log(directory, message));
            _loader.LoadDirectory(Path.Combine(directory, "mods", "dotnet"));
            return 0;
        }
        catch (Exception exception)
        {
            try
            {
                Console.Error.WriteLine(exception);
                Cleanup();
            }
            catch
            {
                // Even cleanup failures must stay on the managed side of this boundary.
            }
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Stop()
    {
        try
        {
            Cleanup();
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static void Cleanup()
    {
        var loader = _loader;
        _loader = null;
        loader?.Dispose();
    }

    private static void Log(string directory, string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(directory, "Net.log"), $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{message}{Environment.NewLine}Logging failed: {exception.Message}");
        }
    }
}
