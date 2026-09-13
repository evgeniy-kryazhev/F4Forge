using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed unsafe class BootstrapTests : IDisposable
{
    private static readonly uint[] LogLevels = new uint[8];
    private static int logCount;

    public void Dispose()
    {
        delegate* unmanaged[Cdecl]<void> shutdown = &Bootstrap.Shutdown;
        shutdown();
    }

    [Fact]
    public void InitializeRejectsNullArguments()
    {
        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;

        Assert.Equal((int)F4ForgeResult.InvalidArgument, initialize(0));
    }

    [Fact]
    public void InitializeValidatesHostVersionAndSize()
    {
        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;
        NativeApi host = CreateHost();
        ManagedBootstrapArgs args = CreateArgs(&host);

        host.AbiVersion = 1;
        Assert.Equal((int)F4ForgeResult.InvalidAbiVersion, initialize((nint)(&args)));

        host.AbiVersion = 3;
        host.StructSize = 159;
        Assert.Equal((int)F4ForgeResult.InvalidStructSize, initialize((nint)(&args)));
    }

    [Fact]
    public void InitializedLoggerForwardsSeverity()
    {
        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;
        NativeApi host = CreateHost();
        ManagedBootstrapArgs args = CreateArgs(&host);
        Assert.Equal((int)F4ForgeResult.Success, initialize((nint)(&args)));

        logCount = 0;
        Logger.Trace("trace");
        Logger.Warning("warning");
        Logger.Error("error");

        Assert.Equal(3, logCount);
        Assert.Equal([0u, 3u, 4u], LogLevels[..3]);
    }

    private static NativeApi CreateHost() => new()
    {
        AbiVersion = 3,
        StructSize = (uint)sizeof(NativeApi),
        ResolveEndpoint = &ResolveEndpoint,
        Invoke = &Invoke,
        Log = &Log
    };

    private static ManagedBootstrapArgs CreateArgs(NativeApi* host) => new()
    {
        AbiVersion = 2,
        StructSize = (uint)sizeof(ManagedBootstrapArgs),
        Host = new NativeHostBinding { AbiVersion = 3, StructSize = (uint)sizeof(NativeHostBinding), Api = host, Context = host },
        Runtime = 1
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ulong ResolveEndpoint(void* context, F4ForgeStringView name, uint version) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Invoke(void* context, ulong endpoint, void* request, uint requestSize, void* response, uint responseCapacity, uint* responseSize)
        => (int)F4ForgeResult.Success;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Log(void* context, uint level, F4ForgeStringView message)
    {
        if (logCount < LogLevels.Length)
        {
            LogLevels[logCount++] = level;
        }
    }
}
