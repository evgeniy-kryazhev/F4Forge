using System.Runtime.InteropServices;

namespace F4Forge.Net.Interop;

// Keep this layout identical to native/core/channel.h. No domain methods belong here.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ChannelBinding
{
    public nint Context;
    public delegate* unmanaged[Cdecl]<nint, byte*, uint, delegate* unmanaged[Cdecl]<nint, byte*, uint, int>, nint, int> Call;
    public delegate* unmanaged[Cdecl]<nint, byte*, uint, delegate* unmanaged[Cdecl]<nint, byte*, uint, delegate* unmanaged[Cdecl]<nint, byte*, uint, int>, nint, int>, nint, ulong> Add;
    public delegate* unmanaged[Cdecl]<nint, ulong, void> Remove;
}
