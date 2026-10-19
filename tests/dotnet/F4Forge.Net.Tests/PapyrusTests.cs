using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Objects;
using F4Forge.Net.Papyrus;
using F4Forge.Net.Serialization;

namespace F4Forge.Net.Tests;

public sealed partial class PapyrusTests
{
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreateBinary")]
    private static partial ChannelBinding CreateBinary();
    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DynamicCallsUseSharedSerializerAndHandleImmediateCompletion(bool binary)
    {
        await using var fixture = new Fixture(binary);
        var context = fixture.Context;
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            Assert.Equal("static", request.GetProperty("operation").GetString());
            Assert.Equal("PrismaUI", request.GetProperty("scriptName").GetString());
            Assert.Equal("Show", request.GetProperty("name").GetString());
            Assert.Equal("Интерфейс Ω", request.GetProperty("arguments")[0].GetProperty("text").GetString());
            fixture.Complete(request, new PapyrusValue { Kind = "bool", Boolean = true });
            return true;
        });
        var result = await context.Papyrus.CallStaticAsync("PrismaUI", "Show", ["Интерфейс Ω"]);
        Assert.True(result.Boolean);
        Assert.Equal(1, fixture.Forgotten);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArgumentsSupportVariantsArraysStructsAndScopedForms(bool binary)
    {
        await using var fixture = new Fixture(binary);
        var form = new Form(fixture.Context.Messages, new(0x14, 7, "ACHR", "Игрок", "Player", true, true, false));
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            var args = request.GetProperty("arguments");
            Assert.Equal("none", args[0].GetProperty("kind").GetString());
            Assert.True(args[1].GetProperty("boolean").GetBoolean());
            Assert.Equal(-5, args[2].GetProperty("integer").GetInt32());
            Assert.Equal(1.5f, args[3].GetProperty("number").GetSingle());
            Assert.Equal(0x14U, args[4].GetProperty("reference").GetProperty("formId").GetUInt32());
            Assert.Equal(7UL, args[4].GetProperty("reference").GetProperty("session").GetUInt64());
            Assert.Equal("array", args[5].GetProperty("kind").GetString());
            Assert.Equal(2, args[5].GetProperty("items").GetArrayLength());
            Assert.Equal("UI#MenuData", args[6].GetProperty("typeName").GetString());
            Assert.Equal(4, args[6].GetProperty("fields").GetProperty("depth").GetProperty("integer").GetInt32());
            fixture.Complete(request, new PapyrusValue { Kind = "string", Text = "готово 🌍" });
            return true;
        });
        var result = await fixture.Context.Papyrus.CallStaticAsync("Probe", "Values",
            [null, true, -5, 1.5, form, new object?[] { 1, "two" },
                new PapyrusStruct("UI#MenuData", new Dictionary<string, object?> { ["depth"] = 4 })]);
        Assert.Equal("готово 🌍", result.Text);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Context.Papyrus.CallStaticAsync("Probe", "Values", [float.NaN]));
        var foreign = new Form(new ModContext(Path.GetTempPath(), fixture.Channel, fixture.Channel, fixture.Channel.Serializer).Messages, form.Snapshot);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Context.Papyrus.CallStaticAsync("Probe", "Values", [foreign]));
    }

    [Fact]
    public async Task MethodsAndPropertiesUseTheSameDynamicProtocol()
    {
        await using var fixture = new Fixture(false);
        var form = new Form(fixture.Context.Messages, new(1, 7, "QUST", "Quest", "Quest", false, false, false));
        var target = fixture.Context.Papyrus.Object(form, "MyQuestScript");
        var operations = new List<string>();
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            operations.Add(request.GetProperty("operation").GetString()!);
            Assert.Equal(1U, request.GetProperty("target").GetProperty("formId").GetUInt32());
            Assert.Equal("MyQuestScript", request.GetProperty("scriptName").GetString());
            fixture.Complete(request, new PapyrusValue { Kind = "int", Integer = 42 });
            return true;
        });
        Assert.Equal(42, (await fixture.Context.Papyrus.CallAsync(target, "GetStage")).Integer);
        Assert.Equal(42, (await fixture.Context.Papyrus.GetPropertyAsync(target, "Counter")).Integer);
        await fixture.Context.Papyrus.SetPropertyAsync(target, "Counter", 10);
        Assert.Equal(new[] { "method", "get", "set" }, operations);
    }

    [Fact]
    public async Task ConcurrentCallsCorrelateResultsAndCopyBorrowedBuffers()
    {
        await using var fixture = new Fixture(false);
        var requests = new List<JsonElement>();
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            requests.Add(request.Clone());
            return true;
        });
        var first = fixture.Context.Papyrus.CallStaticAsync("Probe", "First");
        var second = fixture.Context.Papyrus.CallStaticAsync("Probe", "Second");
        fixture.Complete(requests[1], new PapyrusValue { Kind = "string", Text = "second" });
        fixture.Complete(requests[0], new PapyrusValue { Kind = "string", Text = "first" });
        Assert.Equal("first", (await first).Text);
        Assert.Equal("second", (await second).Text);
    }

    [Fact]
    public async Task CancellationAndShutdownDetachPendingWorkWithoutWaitingForVm()
    {
        await using var fixture = new Fixture(false);
        var requests = new List<JsonElement>();
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            requests.Add(request.Clone());
            return true;
        });
        using var cancellation = new CancellationTokenSource();
        var canceled = fixture.Context.Papyrus.CallStaticAsync("Probe", "Wait", cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.Equal(1, fixture.Forgotten);
        var pending = fixture.Context.Papyrus.CallStaticAsync("Probe", "Wait");
        fixture.Lifetime.Close();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await fixture.Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, fixture.Released);
        fixture.Complete(requests[1], new PapyrusValue { Kind = "int", Integer = 99 });
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Context.Papyrus.CallStaticAsync("Probe", "Wait"));
    }

    [Fact]
    public async Task ErrorsAndPreCanceledCallsDoNotLeavePendingRegistrations()
    {
        await using var fixture = new Fixture(false);
        var calls = 0;
        using var begin = fixture.Channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            calls++;
            fixture.Channel.Publish("papyrus.completed", new
            {
                RequestId = request.GetProperty("requestId").GetString(), Success = false,
                Value = (PapyrusValue?)null, Error = "Нет функции Ω"
            });
            return true;
        });
        var error = await Assert.ThrowsAsync<MessageException>(() => fixture.Context.Papyrus.CallStaticAsync("Probe", "Unknown"));
        Assert.Equal("Нет функции Ω", error.Message);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Context.Papyrus.CallStaticAsync("Probe", "Wait", cancellationToken: cancellation.Token));
        Assert.Equal(1, calls);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Context.Papyrus.CallStaticAsync(" ", "Function"));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Context.Papyrus.CallStaticAsync("Probe", "bad\0name"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly IDisposable _forget;
        private readonly IDisposable _release;
        internal NativeChannel Channel { get; }
        internal ModLifetime Lifetime { get; } = new();
        internal ModContext Context { get; }
        internal int Forgotten { get; private set; }
        internal int Released { get; private set; }

        internal Fixture(bool binary)
        {
            Channel = binary ? new NativeChannel(CreateBinary(), new TaggedSerializer()) : new NativeChannel(Create());
            Context = new ModContext(Path.GetTempPath(), Channel, Channel, Channel.Serializer, lifetime: Lifetime);
            _forget = Channel.Register<JsonElement, bool>("papyrus.forget", _ =>
            {
                Forgotten++;
                return true;
            });
            _release = Channel.Register<JsonElement, bool>("papyrus.release", _ =>
            {
                Released++;
                return true;
            });
        }

        internal void Complete(JsonElement request, PapyrusValue value)
        {
            Channel.Publish("papyrus.completed", new
            {
                RequestId = request.GetProperty("requestId").GetString(), Success = true, Value = value, Error = (string?)null
            });
        }

        public async ValueTask DisposeAsync()
        {
            Lifetime.Close();
            await Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            _release.Dispose();
            _forget.Dispose();
            Channel.DetachTransport();
            Destroy();
        }
    }

    private sealed class TaggedSerializer : IDataSerializer
    {
        private readonly JsonDataSerializer _json = new();
        public string Format => "test.binary";
        public byte[] Serialize<T>(T value) => [0, 255, .. _json.Serialize(value)];
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
