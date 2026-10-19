using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.Net.Interop;

namespace F4Forge.Net.Hosting;

internal static unsafe class Bootstrap
{
    private static ModLoader? _loader;
    private static readonly object Gate = new();
    private static Task _cleanup = Task.CompletedTask;
    private static NativeChannel? _channel;

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
            if (arguments is null || arguments->Directory is null
                || arguments->Channel.Context == 0 || arguments->Channel.Call is null
                || arguments->Channel.Add is null || arguments->Channel.Remove is null)
            {
                return 1;
            }
            lock (Gate)
            {
                if (_loader is not null || !_cleanup.IsCompleted)
                {
                    return 1;
                }
                var pluginDirectory = Path.GetFullPath(new string(arguments->Directory));
                var directory = Path.Combine(pluginDirectory, "F4Forge");
                System.IO.Directory.CreateDirectory(directory);
                var channel = new NativeChannel(arguments->Channel);
                _channel = channel;
                _loader = new ModLoader(channel, channel, channel.Serializer,
                    message => Log(directory, message), message => Log(directory, message));
                var modDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, "..", "..", "F4ForgeNet"));
                _loader.LoadDirectory(modDirectory);
            }
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
        lock (Gate)
        {
            var loader = _loader;
            if (loader is null)
            {
                return;
            }
            loader.Dispose();
            _channel?.DetachTransport();
            _channel = null;
            _cleanup = loader.Completion;
            _loader = null;
        }
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
