using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using F4Forge.DotNet.Sdk;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Plugins;

namespace F4Forge.DotNet.Runtime;

public static unsafe class Bootstrap
{
    private enum LifecycleState : uint
    {
        Stopped,
        Initializing,
        Running,
        ShuttingDown,
        Failed
    }

    private const uint RuntimeProviderAbiVersion = 2;
    private const uint HostAbiVersion = 3;
    private const uint ManagedBootstrapMinimumSize = 72;
    private const uint HostBindingSize = 24;
    private const uint NativeApiMinimumSize = 160;
    private static readonly object Gate = new();
    private static NativeApi* nativeApi;
    private static void* nativeContext;
    private static ulong runtimeHandle;
    private static LifecycleState state;
    private static PluginLoader? pluginLoader;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Initialize(nint apiPointer)
    {
        try
        {
            if (apiPointer == 0) return (int)F4ForgeResult.InvalidArgument;
            var args = (ManagedBootstrapArgs*)apiPointer;
            if (args->AbiVersion != RuntimeProviderAbiVersion) return (int)F4ForgeResult.InvalidAbiVersion;
            if (args->StructSize < ManagedBootstrapMinimumSize) return (int)F4ForgeResult.InvalidStructSize;
            if (args->Runtime == 0) return (int)F4ForgeResult.InvalidArgument;
            if (args->Host.AbiVersion != HostAbiVersion)
                return (int)F4ForgeResult.InvalidAbiVersion;
            if (args->Host.StructSize < HostBindingSize || args->Host.Api == null || args->Host.Context == null)
                return (int)F4ForgeResult.InvalidArgument;
            if (args->Host.Api->AbiVersion != HostAbiVersion)
                return (int)F4ForgeResult.InvalidAbiVersion;
            if (args->Host.Api->StructSize < NativeApiMinimumSize)
                return (int)F4ForgeResult.InvalidStructSize;
            if (args->Host.Api->ResolveEndpoint == null || args->Host.Api->Invoke == null)
                return (int)F4ForgeResult.InvalidArgument;

            PluginLoader loader;
            string pluginDirectory;
            lock (Gate)
            {
                if (state == LifecycleState.Running) return (int)F4ForgeResult.Success;
                if (state is LifecycleState.Initializing or LifecycleState.ShuttingDown)
                    return (int)F4ForgeResult.InactiveRuntime;
                nativeApi = args->Host.Api;
                nativeContext = args->Host.Context;
                runtimeHandle = args->Runtime;
                Logger.Sink = (level, message) => WriteLog(nativeApi, nativeContext, level, message);
                pluginDirectory = ReadUtf8(args->PluginDirectory);
                 loader = new PluginLoader(host: args->Host.Api, hostContext: args->Host.Context, runtime: args->Runtime);
                pluginLoader = loader;
                state = LifecycleState.Initializing;
            }
            Logger.Info($"Managed plugin directory: {pluginDirectory}");
            var loadedPlugins = loader.LoadDirectory(pluginDirectory);
            lock (Gate)
            {
                if (state != LifecycleState.Initializing) return (int)F4ForgeResult.InactiveRuntime;
                state = LifecycleState.Running;
            }
            Logger.Info($"Managed plugins loaded: {loadedPlugins}");
            return (int)F4ForgeResult.Success;
        }
        catch
        {
            lock (Gate) state = LifecycleState.Failed;
            return (int)F4ForgeResult.InternalError;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void ExecuteTask(ulong runtime, ulong taskId)
    {
        try
        {
            PluginLoader? loader;
            lock (Gate)
            {
                if (state != LifecycleState.Running || runtime != runtimeHandle) return;
                loader = pluginLoader;
            }
            loader?.ExecuteTask(taskId);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static void Shutdown()
    {
        try
        {
            PluginLoader? loader;
            lock (Gate)
            {
                if (state is LifecycleState.Stopped or LifecycleState.ShuttingDown) return;
                state = LifecycleState.ShuttingDown;
                loader = pluginLoader;
                pluginLoader = null;
            }
            loader?.Dispose();
            lock (Gate)
            {
                Logger.Sink = null;
                nativeApi = null;
                nativeContext = null;
                runtimeHandle = 0;
                state = LifecycleState.Stopped;
            }
        }
        catch
        {
            lock (Gate) state = LifecycleState.Failed;
        }
    }

    internal static bool IsInitialized
    {
        get { lock (Gate) return state == LifecycleState.Running; }
    }

    private static string ReadUtf8(F4ForgeStringView value)
    {
        if (value.Data == null || value.Length == 0) return string.Empty;
        return Encoding.UTF8.GetString(value.Data, checked((int)value.Length));
    }

    private static void WriteLog(NativeApi* api, void* context, F4ForgeLogLevel level, string message)
    {
        if (api == null || api->Log == null) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        fixed (byte* text = bytes)
        {
            var view = new F4ForgeStringView { Data = text, Length = checked((uint)bytes.Length) };
            api->Log(context, (uint)level, view);
        }
    }
}
