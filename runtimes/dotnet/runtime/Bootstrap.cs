using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

public static unsafe class Bootstrap
{
    private const uint AbiVersion = 1;
    private static readonly object Gate = new();
    private static NativeApi* nativeApi;
    private static bool initialized;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static int Initialize(nint apiPointer)
    {
        try
        {
            if (apiPointer == 0) return (int)F4ForgeResult.InvalidArgument;
            var candidate = (NativeApi*)apiPointer;
            if (candidate->AbiVersion != AbiVersion) return (int)F4ForgeResult.InvalidAbiVersion;
            if (candidate->StructSize < sizeof(NativeApi)) return (int)F4ForgeResult.InvalidStructSize;
            if (candidate->ResolveEndpoint == null || candidate->Invoke == null) return (int)F4ForgeResult.InvalidArgument;

            lock (Gate)
            {
                if (initialized) return (int)F4ForgeResult.Success;
                nativeApi = candidate;
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
}
