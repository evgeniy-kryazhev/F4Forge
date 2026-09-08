using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

public static unsafe class Bootstrap
{
    private const uint AbiVersion = 1;
    private static readonly object Gate = new();
    private static NativeApi* nativeApi;
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
            if (args->StructSize < sizeof(ManagedBootstrapArgs)) return (int)F4ForgeResult.InvalidStructSize;
            if (args->Host == null) return (int)F4ForgeResult.InvalidArgument;
            if (args->Host->AbiVersion != AbiVersion || args->Host->StructSize < sizeof(NativeApi))
                return (int)F4ForgeResult.InvalidStructSize;
            if (args->Host->ResolveEndpoint == null || args->Host->Invoke == null)
                return (int)F4ForgeResult.InvalidArgument;

            lock (Gate)
            {
                if (initialized) return (int)F4ForgeResult.Success;
                nativeApi = args->Host;
                var pluginDirectory = ReadUtf8(args->PluginDirectory);
                pluginLoader = new PluginLoader();
                pluginLoader.LoadDirectory(pluginDirectory);
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
    public static void ExecuteTask(ulong _)
    {
        try
        {
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
}
