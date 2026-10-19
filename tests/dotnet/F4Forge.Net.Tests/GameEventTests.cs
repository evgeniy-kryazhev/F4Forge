using System.Runtime.InteropServices;
using F4Forge.Net.Events;
using F4Forge.Net.Actors.Events;
using F4Forge.Net.Game.Events;
using F4Forge.Net.Player.Events;
using F4Forge.Net.Saves.Events;
using F4Forge.Net.UI.Events;
using F4Forge.Net.Interop;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Tests;

public sealed partial class GameEventTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void RuntimeLoadedUsesNativeWireContract()
    {
        Verify("game.runtime.loaded", new { },
            new RuntimeLoaded());
    }

    [Fact]
    public void GameDataReadyUsesNativeWireContract()
    {
        Verify("game.data.ready", new { ready = true },
            new GameDataReady(true));
    }

    [Fact]
    public void NewGameUsesNativeWireContract()
    {
        Verify("game.new", new { },
            new NewGame());
    }

    [Fact]
    public void SaveLoadingUsesNativeWireContract()
    {
        Verify("game.save.loading", new { saveName = "Save Ω" },
            new SaveLoading("Save Ω"));
    }

    [Fact]
    public void SaveLoadedUsesNativeWireContract()
    {
        Verify("game.save.loaded", new { saveName = "Save Ω", success = false },
            new SaveLoaded("Save Ω", false));
    }

    [Fact]
    public void SaveSavingUsesNativeWireContract()
    {
        Verify("game.save.saving", new { saveName = "Save Ω" },
            new SaveSaving("Save Ω"));
    }

    [Fact]
    public void SaveSavedUsesNativeWireContract()
    {
        Verify("game.save.saved", new { saveName = "Save Ω" },
            new SaveSaved("Save Ω"));
    }

    [Fact]
    public void ActorDiedUsesNativeWireContract()
    {
        Verify("game.actor.died", new { actorFormId = 0x14u, killerFormId = 0u, isPlayer = true },
            new ActorDied(0x14, 0, true));
    }

    [Fact]
    public void PlayerCellChangedUsesNativeWireContract()
    {
        Verify("game.player.cell.changed", new { previousCellFormId = 0u, newCellFormId = 0x1234u },
            new PlayerCellChanged(0, 0x1234));
    }

    [Fact]
    public void PlayerLocationChangedUsesNativeWireContract()
    {
        Verify("game.player.location.changed", new { previousLocationFormId = 0x1234u, newLocationFormId = 0u },
            new PlayerLocationChanged(0x1234, 0));
    }

    [Fact]
    public void MenuChangedUsesNativeWireContract()
    {
        Verify("game.menu.changed", new { menuName = "PipboyMenu Ω", opening = true },
            new MenuChanged("PipboyMenu Ω", true));
    }

    [Fact]
    public void ScopedSubscriptionsStopWithTheirMod()
    {
        var lifetime = new ModLifetime();
        using var events = new ScopedEvents(_channel, lifetime);
        var context = new ModContext(@"C:\mods\Example", _channel, events, _channel.Serializer);
        var count = 0;
        context.Events.Subscribe<ActorDied>(_ => count++);
        _channel.Publish("game.actor.died", new ActorDied(0x14, 0, true));
        events.Dispose();
        _channel.Publish("game.actor.died", new ActorDied(0x14, 0, true));
        Assert.Equal(1, count);
    }

    [Fact]
    public void SubscriberFailureDoesNotBlockLaterGameSubscribers()
    {
        var count = 0;
        using var failing = _channel.Subscribe<PlayerLocationChanged>((Action<PlayerLocationChanged>)(_ => throw new InvalidOperationException("Mod failed")));
        using var healthy = _channel.Subscribe<PlayerLocationChanged>(_ => count++);
        _channel.Publish("game.player.location.changed", new PlayerLocationChanged(0, 42));
        Assert.Equal(1, count);
    }

    [Fact]
    public void AbsentSaveNameAndFailedLoadRemainDistinct()
    {
        SaveLoaded? notification = null;
        using var subscription = _channel.Subscribe<SaveLoaded>(value => notification = value);
        _channel.Publish("game.save.loaded", new { saveName = (string?)null, success = false });
        Assert.Equal(new SaveLoaded(null, false), notification);
    }

    [Fact]
    public void TypedSubscriptionsValidateChannelAndHandler()
    {
        Assert.Throws<ArgumentNullException>(() => GameEventExtensions.Subscribe<SaveLoaded>(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => _channel.Subscribe<SaveLoaded>((Action<SaveLoaded>)null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Subscribe<SaveLoaded>((Func<SaveLoaded, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => _channel.Subscribe<SaveLoaded>((Func<SaveLoaded, CancellationToken, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => GameEventExtensions.Publish<SaveLoaded>(null!, new SaveLoaded(null, false)));
        Assert.Throws<ArgumentNullException>(() => _channel.Publish<SaveLoaded>(null!));
    }

    [Fact]
    public void TypedPublicationUsesTheSameNameWithoutSerializingMetadata()
    {
        System.Text.Json.JsonElement payload = default;
        using var subscriber = _channel.Subscribe<System.Text.Json.JsonElement>("game.save.loaded", value => payload = value);
        _channel.Publish(new SaveLoaded("Save Ω", true));
        Assert.Equal("Save Ω", payload.GetProperty("saveName").GetString());
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.False(payload.TryGetProperty("eventName", out _));
    }

    private void Verify<T>(string name, object nativePayload, T expected) where T : class, IGameEvent
    {
        T? received = null;
        var count = 0;
        using var subscription = _channel.Subscribe<T>(notification =>
        {
            received = notification;
            count++;
        });
        _channel.Publish("unrelated.event", nativePayload);
        Assert.Null(received);
        _channel.Publish(name, nativePayload);
        Assert.Equal(expected, received);
        subscription.Dispose();
        _channel.Publish(name, nativePayload);
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        Destroy();
    }
}
