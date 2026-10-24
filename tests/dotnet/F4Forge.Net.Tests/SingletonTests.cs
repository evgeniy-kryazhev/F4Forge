using System.Runtime.InteropServices;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Serialization;
using F4Forge.Net.Singletons;
using GameUI = F4Forge.Net.Singletons.UI;

namespace F4Forge.Net.Tests;

public sealed partial class SingletonTests : IDisposable
{
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void ProxiesAreTypedCachedAndScopedToContextWithoutEngineAccess()
    {
        var channel = new NativeChannel(Create());
        var first = Context(channel);
        var second = Context(channel);

        Assert.Same(first.GetSingleton<PlayerControls>(), first.GetSingleton<PlayerControls>());
        Assert.NotSame(first.GetSingleton<PlayerControls>(), second.GetSingleton<PlayerControls>());
        Assert.IsType<PlayerCamera>(first.GetSingleton<PlayerCamera>());
        Assert.IsType<GameUI>(first.GetSingleton<GameUI>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedPropertyUsesSharedSerializerAndReadsCurrentState(bool binary)
    {
        var channel = binary
            ? new NativeChannel(CreateBinary(), new TaggedSerializer())
            : new NativeChannel(Create());
        var context = Context(channel);
        var available = false;
        var blocked = false;
        using var availability = channel.Register<object?, bool>("singletons.RE::PlayerControls.available", _ => available);
        using var property = channel.Register<object?, bool>("singletons.RE::PlayerControls.blockPlayerInput.get", _ => blocked);
        var controls = context.GetSingleton<PlayerControls>();

        Assert.False(controls.IsAvailable);
        available = true;
        Assert.True(controls.IsAvailable);
        Assert.False(controls.BlockPlayerInput);
        blocked = true;
        Assert.True(controls.BlockPlayerInput);
    }

    [Fact]
    public void GeneratedProxyPreservesNativeFailureAndUnregistration()
    {
        var channel = new NativeChannel(Create());
        var controls = Context(channel).GetSingleton<PlayerControls>();
        var registration = channel.Register<object?, bool>("singletons.RE::PlayerControls.blockPlayerInput.get", _ =>
        {
            throw new InvalidOperationException("Engine singleton is unavailable: RE::PlayerControls");
        });

        var exception = Assert.Throws<MessageException>(() => controls.BlockPlayerInput);
        Assert.Equal("handler_failed", exception.Code);
        Assert.Contains("unavailable", exception.Message);
        registration.Dispose();
        Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => controls.BlockPlayerInput).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedStringEnumAndVoidMethodsUseTypedArguments(bool binary)
    {
        var channel = binary
            ? new NativeChannel(CreateBinary(), new TaggedSerializer())
            : new NativeChannel(Create());
        var context = Context(channel);
        const string menuName = "PauseMenu — Меню 🎮";
        using var menu = channel.Register<MenuArgument, bool>("singletons.RE::UI.GetMenuOpen.aadd64abb67a", arguments =>
        {
            Assert.Equal(menuName, arguments.Name);
            return true;
        });
        using var cameraState = channel.Register<StateArgument, bool>("singletons.RE::PlayerCamera.QCameraEquals.f5f993b084d2", arguments =>
        {
            Assert.Equal(CameraStates_CameraState.kFree, arguments.State);
            return true;
        });
        var voidCalls = 0;
        using var toggle = channel.Register<FreezeArgument, bool>("singletons.RE::PlayerCamera.ToggleFreeCameraMode.6bc8e0f1418d", arguments =>
        {
            Assert.True(arguments.FreezeTime);
            voidCalls++;
            return true;
        });

        Assert.True(context.GetSingleton<GameUI>().GetMenuOpen(menuName));
        Assert.True(context.GetSingleton<PlayerCamera>().QCameraEquals(CameraStates_CameraState.kFree));
        context.GetSingleton<PlayerCamera>().ToggleFreeCameraMode(true);
        Assert.Equal(1, voidCalls);
        Assert.Throws<ArgumentNullException>(() => context.GetSingleton<GameUI>().GetMenuOpen(null!));
        Assert.Throws<ArgumentException>(() => context.GetSingleton<GameUI>().GetMenuOpen("a\0b"));
    }

    private sealed record MenuArgument(string Name);
    private sealed record StateArgument(CameraStates_CameraState State);
    private sealed record FreezeArgument(bool FreezeTime);

    private static ModContext Context(NativeChannel channel)
    {
        return new ModContext(@"C:\mods\SingletonTests", channel, channel, channel.Serializer);
    }

    public void Dispose()
    {
        Destroy();
    }

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public string Format => "test.binary";

        public byte[] Serialize<T>(T value)
        {
            return [0, 255, .. _json.Serialize(value)];
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            if (data.Length < 2 || data[0] != 0 || data[1] != 255)
            {
                throw new InvalidDataException("Missing binary prefix.");
            }
            return _json.Deserialize<T>(data[2..]);
        }
    }
}
