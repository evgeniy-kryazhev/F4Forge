using System.Runtime.InteropServices;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Operations;
using F4Forge.Net.Serialization;
using F4Forge.Net.UI;
using F4Forge.Net.UI.Queries;

namespace F4Forge.Net.Tests;

public sealed partial class UIQueryTests : IDisposable
{
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void QueryAndExtensionPreserveBooleanResult(bool isOpen, bool binary)
    {
        var channel = binary
            ? new NativeChannel(CreateBinary(), new TaggedSerializer())
            : new NativeChannel(Create());
        var calls = 0;
        using var registration = channel.Register<object?, bool>("ui.is_pause_menu_open", arguments =>
        {
            Assert.Null(arguments);
            calls++;
            return isOpen;
        });
        var context = new ModContext(@"C:\mods\UIQuery", channel, channel, channel.Serializer);
        var query = new IsPauseMenuOpen();

        Assert.Equal("ui.is_pause_menu_open", query.MethodName);
        Assert.Null(query.Arguments);
        Assert.Equal(isOpen, context.Messages.Query(query));
        Assert.Equal(isOpen, context.IsPauseMenuOpen());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ExtensionPreservesChannelFailure()
    {
        var channel = new NativeChannel(Create());
        using var registration = channel.Register<object?, bool>("ui.is_pause_menu_open", _ =>
        {
            throw new InvalidOperationException("UI queries must execute on the game thread.");
        });
        var context = new ModContext(@"C:\mods\UIQuery", channel, channel, channel.Serializer);

        var exception = Assert.Throws<MessageException>(() => context.IsPauseMenuOpen());

        Assert.Equal("handler_failed", exception.Code);
        Assert.Contains("game thread", exception.Message);
    }

    [Fact]
    public void ExtensionRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => UIExtensions.IsPauseMenuOpen(null!));
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
