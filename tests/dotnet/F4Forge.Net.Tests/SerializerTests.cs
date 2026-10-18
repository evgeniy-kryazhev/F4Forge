using System.Runtime.InteropServices;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Tests;

public sealed partial class SerializerTests : IDisposable
{
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void OneSerializerHandlesMessagesEventsCallbacksAndErrors()
    {
        var serializer = new TaggedSerializer();
        var channel = new NativeChannel(CreateBinary(), serializer);
        Assert.Same(serializer, channel.Serializer);
        using var method = channel.Register<Payload, Payload>("managed.echo", arguments => arguments);
        var payload = new Payload("Привет 🌍", [1, 2, 3]);
        Assert.Equal(payload.Text, channel.Call<Payload>("native.echo", payload).Text);
        Assert.Equal(payload.Numbers, channel.Call<Payload>("managed.echo", payload).Numbers);
        Payload? received = null;
        using var subscription = channel.Subscribe<Payload>("test.event", value => received = value);
        channel.Publish("test.event", payload);
        Assert.Equal(payload.Text, received?.Text);
        using var broken = channel.Register<object?, int>("managed.fail", _ => throw new InvalidOperationException("Encoded error"));
        var error = Assert.Throws<MessageException>(() => channel.Call<int>("managed.fail", null));
        Assert.Equal("handler_failed", error.Code);
        Assert.Equal("Encoded error", error.Message);
        Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => channel.Call<int>("missing", null)).Code);
        Assert.True(serializer.Writes >= 8);
        Assert.True(serializer.Reads >= 8);
    }

    [Fact]
    public void MismatchedFormatIsRejectedBeforeAnyNativeCall()
    {
        var binding = CreateBinary();
        Assert.Throws<ArgumentException>(() => new NativeChannel(binding, new JsonDataSerializer()));
        Assert.Throws<NotSupportedException>(() => new NativeChannel(binding));
    }

    [Fact]
    public void JsonSerializerDeserializesTypedValuesWithoutBorrowingInput()
    {
        var serializer = new JsonDataSerializer();
        var bytes = serializer.Serialize(new Payload("Text", [1, 2]));
        var result = serializer.Deserialize<Payload>(bytes);
        Array.Clear(bytes);
        Assert.Equal("Text", result.Text);
        Assert.Equal([1, 2], result.Numbers);
    }

    public void Dispose()
    {
        Destroy();
    }

    public sealed record Payload(string Text, int[] Numbers);

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public int Writes { get; private set; }
        public int Reads { get; private set; }
        public string Format => "test.binary";

        public byte[] Serialize<T>(T value)
        {
            Writes++;
            return [0, 255, .. _json.Serialize(value)];
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            Reads++;
            if (data.Length < 2 || data[0] != 0 || data[1] != 255)
            {
                throw new InvalidDataException("Missing binary prefix.");
            }
            return _json.Deserialize<T>(data[2..]);
        }
    }
}
