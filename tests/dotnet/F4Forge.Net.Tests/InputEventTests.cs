using System.Runtime.InteropServices;
using F4Forge.Net.Events;
using F4Forge.Net.Input.Events;
using F4Forge.Net.Interop;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Tests;

public sealed partial class InputEventTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Theory]
    [InlineData(InputDevice.Keyboard, 30U, KeyCode.A)]
    [InlineData(InputDevice.Mouse, 256U, KeyCode.MouseLeft)]
    [InlineData(InputDevice.Gamepad, 276U, KeyCode.GamepadA)]
    public void PressedUsesNativeWireContract(InputDevice device, uint keyCode, KeyCode expectedKey)
    {
        Verify("game.input.button.pressed", new
        {
            device = (int)device,
            keyCode,
            userEvent = "Action Ω",
            heldSeconds = 0.0F
        }, new ButtonPressed(device, expectedKey, "Action Ω", 0.0F));
    }

    [Theory]
    [InlineData(InputDevice.Keyboard, 30U, KeyCode.A)]
    [InlineData(InputDevice.Mouse, 263U, KeyCode.MouseButton8)]
    [InlineData(InputDevice.Gamepad, 281U, KeyCode.GamepadRightTrigger)]
    public void ReleasedUsesNativeWireContract(InputDevice device, uint keyCode, KeyCode expectedKey)
    {
        Verify("game.input.button.released", new
        {
            device = (int)device,
            keyCode,
            userEvent = "",
            heldSeconds = 2.5F
        }, new ButtonReleased(device, expectedKey, "", 2.5F));
    }

    [Fact]
    public void SubscriberFailureDoesNotBlockOtherInputSubscribers()
    {
        var deliveries = 0;
        using var failing = _channel.Subscribe<ButtonPressed>((Action<ButtonPressed>)(_ =>
        {
            throw new InvalidOperationException("Input handler failed");
        }));
        using var healthy = _channel.Subscribe<ButtonPressed>(_ =>
        {
            deliveries++;
        });
        _channel.Publish(new ButtonPressed(InputDevice.Keyboard, KeyCode.A, "", 0));
        Assert.Equal(1, deliveries);
    }

    [Fact]
    public void InputSubscriptionsStopWithTheirMod()
    {
        var lifetime = new ModLifetime();
        using var events = new ScopedEvents(_channel, lifetime);
        var presses = 0;
        var releases = 0;
        events.Subscribe<ButtonPressed>(_ =>
        {
            presses++;
        });
        events.Subscribe<ButtonReleased>(_ =>
        {
            releases++;
        });
        var pressed = new ButtonPressed(InputDevice.Keyboard, KeyCode.A, "", 0);
        var released = new ButtonReleased(InputDevice.Keyboard, KeyCode.A, "", 1);
        _channel.Publish(pressed);
        _channel.Publish(released);
        events.Dispose();
        _channel.Publish(pressed);
        _channel.Publish(released);
        Assert.Equal(1, presses);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void TypedPublicationUsesCamelCaseWithoutEventMetadata()
    {
        System.Text.Json.JsonElement received = default;
        using var subscription = _channel.Subscribe<System.Text.Json.JsonElement>(ButtonPressed.EventName, value =>
        {
            received = value;
        });
        _channel.Publish(new ButtonPressed(InputDevice.Mouse, KeyCode.MouseLeft, "Fire", 0));
        Assert.Equal(1, received.GetProperty("device").GetInt32());
        Assert.Equal(256U, received.GetProperty("keyCode").GetUInt32());
        Assert.Equal("Fire", received.GetProperty("userEvent").GetString());
        Assert.Equal(0.0F, received.GetProperty("heldSeconds").GetSingle());
        Assert.False(received.TryGetProperty("eventName", out _));
    }

    private void Verify<T>(string name, object wirePayload, T expected) where T : class, IGameEvent
    {
        T? received = null;
        var deliveries = 0;
        using var subscription = _channel.Subscribe<T>(notification =>
        {
            received = notification;
            deliveries++;
        });
        _channel.Publish("unrelated.event", wirePayload);
        Assert.Null(received);
        _channel.Publish(name, wirePayload);
        Assert.Equal(expected, received);
        subscription.Dispose();
        _channel.Publish(name, wirePayload);
        Assert.Equal(1, deliveries);
    }

    public void Dispose()
    {
        Destroy();
    }
}
