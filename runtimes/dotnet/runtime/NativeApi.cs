using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace F4Forge.DotNet.Runtime;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct F4ForgeStringView
{
    public byte* Data;
    public uint Length;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct F4ForgeEndpointDefinition
{
    public uint StructSize;
    public uint Kind;
    public uint Version;
    public uint Flags;
    public uint ThreadPolicy;
    public uint RequestSize;
    public uint ResponseSize;
    public uint PayloadSize;
    public F4ForgeStringView Name;
    public delegate* unmanaged[Cdecl]<void*, void*, uint, void*, uint, uint*, int> Thunk;
    public void* Context;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal unsafe struct NativeApi
{
    public uint AbiVersion;
    public uint StructSize;
    public delegate* unmanaged[Cdecl]<F4ForgeStringView, uint, ulong> ResolveEndpoint;
    public delegate* unmanaged[Cdecl]<ulong, void*, uint, void*, uint, uint*, int> Invoke;
    public delegate* unmanaged[Cdecl]<ulong, delegate* unmanaged[Cdecl]<ulong, ulong, void*, uint, void*, void>, void*, ulong> Subscribe;
    public delegate* unmanaged[Cdecl]<ulong, void> Unsubscribe;
    public delegate* unmanaged[Cdecl]<ulong, delegate* unmanaged[Cdecl]<ulong, ulong, void*, uint, void*, int>, void*, ulong> Intercept;
    public delegate* unmanaged[Cdecl]<ulong, void> RemoveInterceptor;
    public delegate* unmanaged[Cdecl]<ulong, F4ForgeEndpointDefinition*, ulong*, int> RegisterEndpoint;
    public delegate* unmanaged[Cdecl]<ulong, F4ForgeStringView, uint, ulong*, int> RegisterModule;
    public delegate* unmanaged[Cdecl]<ulong, int> UnregisterModule;
    public delegate* unmanaged[Cdecl]<F4ForgeStringView, uint, uint> QueryCapability;
    public delegate* unmanaged[Cdecl]<ulong, ulong, void*, int> QueueTask;
    public delegate* unmanaged[Cdecl]<uint, F4ForgeStringView, void> Log;
}
