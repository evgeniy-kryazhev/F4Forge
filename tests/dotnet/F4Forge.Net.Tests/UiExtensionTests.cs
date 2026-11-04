using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Papyrus;
using F4Forge.Net.Serialization;
using F4Forge.Net.UI;
using F4Forge.Net.Resources;
using System.Text;

namespace F4Forge.Net.Tests;

public sealed partial class UiExtensionTests
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
    public async Task ViewsIsolateOwnersHandleImmediateReadinessAndDetachBeforeLateMessages(bool binary)
    {
        await using var fixture = new Fixture(binary);
        var scopes = new List<string>();
        var released = new List<string>();
        using var create = fixture.Channel.Register<JsonElement, bool>("prisma.create", request =>
        {
            var scope = request.GetProperty("scope").GetString()!;
            scopes.Add(scope);
            fixture.Channel.Publish("prisma.ready", new { Scope = scope });
            return true;
        });
        using var release = fixture.Channel.Register<JsonElement, bool>("prisma.release", request =>
        {
            released.Add(request.GetProperty("scope").GetString()!);
            return true;
        });
        var firstMessages = new List<string>();
        var secondMessages = new List<string>();
        using var first = await fixture.Context.GetSingleton<GamePrismaUi>().CreateAsync("first.html", firstMessages.Add);
        using var second = await fixture.Context.GetSingleton<GamePrismaUi>().CreateAsync("second.html", secondMessages.Add);
        await first.WaitUntilReadyAsync();
        await second.WaitUntilReadyAsync();
        var reloads = 0;
        first.Reloaded += () => reloads++;
        fixture.Channel.Publish("prisma.ready", new { Scope = scopes[0] });
        Assert.Equal(1, reloads);
        fixture.Channel.Publish("prisma.message", new { Scope = scopes[0], Message = "Привет Ω" });
        Assert.Equal("Привет Ω", Assert.Single(firstMessages));
        Assert.Empty(secondMessages);
        first.Dispose();
        fixture.Channel.Publish("prisma.ready", new { Scope = scopes[0] });
        Assert.Equal(1, reloads);
        fixture.Channel.Publish("prisma.message", new { Scope = scopes[0], Message = "late" });
        Assert.Single(firstMessages);
        Assert.Equal(scopes[0], Assert.Single(released));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await first.ShowAsync());
    }

    [Fact]
    public async Task StartupFailureRemovesSubscriptionsAndDoesNotDestroyAnUncreatedView()
    {
        await using var fixture = new Fixture(false);
        using var create = fixture.Channel.Register<JsonElement, bool>("prisma.create", _ =>
            throw new InvalidOperationException("Renderer failed"));
        var calls = 0;
        await Assert.ThrowsAsync<MessageException>(() =>
            fixture.Context.GetSingleton<GamePrismaUi>().CreateAsync("failed.html", _ => calls++));
        fixture.Channel.Publish("prisma.message", new { Scope = "irrelevant", Message = "late" });
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ShutdownDuringCreationReleasesTheNativeViewBeforeLifetimeCompletion()
    {
        await using var fixture = new Fixture(false);
        using var create = fixture.Channel.Register<JsonElement, bool>("prisma.create", _ =>
        {
            fixture.Lifetime.Close();
            return true;
        });
        var released = 0;
        using var release = fixture.Channel.Register<JsonElement, bool>("prisma.release", _ =>
        {
            released++;
            return true;
        });
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            fixture.Context.GetSingleton<GamePrismaUi>().CreateAsync("closing.html", _ => { }));
        await fixture.Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFunctionRegistrationSupportsNestedCallsAndRevokesCallbackOnClose(bool binary)
    {
        await using var fixture = new Fixture(binary);
        string? callback = null;
        using var register = fixture.Channel.Register<JsonElement, string>("papyrus.functions.register", request =>
        {
            Assert.Equal("Probe", request.GetProperty("script").GetString());
            Assert.Equal("int", request.GetProperty("returnType").GetString());
            callback = request.GetProperty("callback").GetString();
            return "Probe:Count";
        });
        var released = false;
        using var release = fixture.Channel.Register<JsonElement, bool>("papyrus.functions.release", request =>
        {
            Assert.Equal("Probe:Count", request.GetProperty("key").GetString());
            released = true;
            return true;
        });
        using var nested = fixture.Channel.Register<object?, int>("probe.nested", _ => 40);
        using var registration = await fixture.Context.GetSingleton<GamePapyrusFunctions>().RegisterAsync(
            "Probe", "Count", "int", ["int"], arguments =>
                fixture.Context.Messages.Call<int>("probe.nested", null) + arguments[0].GetInt32());
        Assert.Equal(42, fixture.Channel.Call<int>(callback!, new[] { 2 }));
        registration.Dispose();
        Assert.True(released);
        Assert.Throws<MessageException>(() => fixture.Channel.Call<int>(callback!, new[] { 2 }));
    }

    [Fact]
    public async Task ModShutdownRevokesPauseMenuEntryAndPreventsLateSelection()
    {
        await using var fixture = new Fixture(false);
        string? scope = null;
        using var register = fixture.Channel.Register<JsonElement, bool>("ui.pause.register", request =>
        {
            scope = request.GetProperty("scope").GetString();
            return true;
        });
        var released = 0;
        using var release = fixture.Channel.Register<JsonElement, bool>("ui.pause.release", _ =>
        {
            released++;
            return true;
        });
        var selections = 0;
        fixture.Context.GetSingleton<GamePauseMenu>().Register("Example Ω", () =>
        {
            selections++;
            return Task.CompletedTask;
        });
        fixture.Channel.Publish("ui.pause.selected", new { Scope = scope });
        Assert.Equal(1, selections);
        fixture.Lifetime.Close();
        await fixture.Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Channel.Publish("ui.pause.selected", new { Scope = scope });
        Assert.Equal(1, selections);
        Assert.Equal(1, released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResourceTextHonorsUtf16BomThroughTheSharedSerializer(bool binary)
    {
        await using var fixture = new Fixture(binary);
        using var read = fixture.Channel.Register<JsonElement, int[]?>("resources.read", request =>
        {
            Assert.Equal("Interface/Translations/Example_ru.txt", request.GetProperty("path").GetString());
            var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("$TITLE\tНастройки Ω"));
            return bytes.Select(value => (int)value).ToArray();
        });
        var text = await fixture.Context.GetSingleton<GameResources>().ReadTextAsync("Interface/Translations/Example_ru.txt");
        Assert.Equal("$TITLE\tНастройки Ω", text);
    }

    [Fact]
    public async Task ScaleformVoidCallsDiscardOpaqueReturnsAndPreserveArguments()
    {
        await using var fixture = new Fixture(false);
        using var invoke = fixture.Channel.Register<JsonElement, bool>("ui.movie.invoke", request =>
        {
            Assert.Equal("PauseMenu", request.GetProperty("menu").GetString());
            Assert.True(request.GetProperty("discardResult").GetBoolean());
            Assert.Equal("Ω", request.GetProperty("arguments")[0].GetString());
            Assert.Equal(42, request.GetProperty("arguments")[1].GetInt32());
            return true;
        });
        await fixture.Context.GetSingleton<GameScaleform>().InvokeAsync("PauseMenu", "root.f4se.plugins.Probe.Apply", ["Ω", 42]);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly IDisposable _gameThread;
        internal NativeChannel Channel { get; }
        internal ModLifetime Lifetime { get; } = new();
        internal ModContext Context { get; }

        internal Fixture(bool binary)
        {
            Channel = binary ? new NativeChannel(CreateBinary(), new TaggedSerializer()) : new NativeChannel(Create());
            Context = new ModContext(Path.GetTempPath(), Channel, Channel, Channel.Serializer, lifetime: Lifetime);
            _gameThread = Channel.Register<object?, bool>("threading.is_game_thread", _ => true);
        }

        public async ValueTask DisposeAsync()
        {
            Lifetime.Close();
            await Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            _gameThread.Dispose();
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
