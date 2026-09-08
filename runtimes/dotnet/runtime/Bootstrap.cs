using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

public static unsafe class Bootstrap
{
    private const uint AbiVersion = 1;
    private const uint ManagedBootstrapMinimumSize = 56;
    private const uint NativeApiMinimumSize = 104;
    private static readonly object Gate = new();
    private static NativeApi* nativeApi;
    private static ulong runtimeHandle;
    private static bool initialized;
    private static PluginLoader? pluginLoader;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Initialize(nint apiPointer)
    {
        try
        {
            if (apiPointer == 0) return (int)F4ForgeResult.InvalidArgument;
            var args = (ManagedBootstrapArgs*)apiPointer;
            if (args->AbiVersion != AbiVersion) return (int)F4ForgeResult.InvalidAbiVersion;
            if (args->StructSize < ManagedBootstrapMinimumSize) return (int)F4ForgeResult.InvalidStructSize;
            if (args->Runtime == 0) return (int)F4ForgeResult.InvalidArgument;
            if (args->Host == null) return (int)F4ForgeResult.InvalidArgument;
            if (args->Host->AbiVersion != AbiVersion || args->Host->StructSize < NativeApiMinimumSize)
                return (int)F4ForgeResult.InvalidStructSize;
            if (args->Host->ResolveEndpoint == null || args->Host->Invoke == null)
                return (int)F4ForgeResult.InvalidArgument;

            lock (Gate)
            {
                if (initialized) return (int)F4ForgeResult.Success;
                nativeApi = args->Host;
                runtimeHandle = args->Runtime;
                Logger.Sink = message => WriteLog(nativeApi, message);
                var pluginDirectory = ReadUtf8(args->PluginDirectory);
                Logger.Info($"Managed plugin directory: {pluginDirectory}");
                pluginLoader = new PluginLoader();
                var loadedPlugins = pluginLoader.LoadDirectory(pluginDirectory);
                Logger.Info($"Managed plugins loaded: {loadedPlugins}");
                initialized = true;
            }
            return (int)F4ForgeResult.Success;
        }
        catch
        {
            return (int)F4ForgeResult.InternalError;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void ExecuteTask(ulong runtime, ulong _)
    {
        try
        {
            if (!initialized || runtime != runtimeHandle) return;
        }
        catch
        {
        }
    }

    internal static bool IsInitialized => initialized;

    private static string ReadUtf8(F4ForgeStringView value)
    {
        if (value.Data == null || value.Length == 0) return string.Empty;
        return Encoding.UTF8.GetString(value.Data, checked((int)value.Length));
    }

    private static void WriteLog(NativeApi* api, string message)
    {
        if (api == null || api->Log == null) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        fixed (byte* text = bytes)
        {
            var view = new F4ForgeStringView { Data = text, Length = checked((uint)bytes.Length) };
            api->Log(2, view);
        }
    }
}
