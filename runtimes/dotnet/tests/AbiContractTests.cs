using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed unsafe class AbiContractTests
{
    [Fact]
    public void NativeStructuresHaveExpectedLayout()
    {
        Assert.Equal(168, sizeof(NativeApi));
        Assert.Equal(64, sizeof(F4ForgeEndpointDefinition));
        Assert.Equal(16, sizeof(F4ForgeStringView));
        Assert.Equal(56, sizeof(ManagedBootstrapArgs));
        Assert.Equal(28, sizeof(F4ForgeKeyEventData));

        Assert.Equal(0, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.AbiVersion)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.StructSize)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.ResolveEndpoint)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.Invoke)).ToInt32());
        Assert.Equal(104, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.InvokeAsync)).ToInt32());
        Assert.Equal(152, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.ReleaseOperation)).ToInt32());
        Assert.Equal(160, Marshal.OffsetOf<NativeApi>(nameof(NativeApi.WaitModuleQuiescence)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<ManagedBootstrapArgs>(nameof(ManagedBootstrapArgs.Host)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<ManagedBootstrapArgs>(nameof(ManagedBootstrapArgs.Runtime)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Name)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Thunk)).ToInt32());
        Assert.Equal(56, Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Context)).ToInt32());
    }

    [Fact]
    public void KeyEventAndKeyEnumHaveExpectedLayout()
    {
        Assert.Equal(0, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.StructSize)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.DeviceType)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.KeyCode)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsDown)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsRepeat)).ToInt32());
        Assert.Equal(20, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.HeldSeconds)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsMenu)).ToInt32());
        Assert.Equal(0u, (uint)Key.Unknown);
        Assert.Equal(0x41u, (uint)Key.A);
        Assert.Equal(0x7Bu, (uint)Key.F12);
        Assert.Equal(0xDEu, (uint)Key.Apostrophe);
    }
}
