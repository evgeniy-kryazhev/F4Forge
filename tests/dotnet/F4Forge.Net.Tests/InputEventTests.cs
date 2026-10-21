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
    [InlineData(InputDevice.Keyboard, 65U, KeyCode.A)]
    [InlineData(InputDevice.Keyboard, 87U, KeyCode.W)]
    [InlineData(InputDevice.Keyboard, 27U, KeyCode.Escape)]
    [InlineData(InputDevice.Mouse, 256U, KeyCode.MouseLeft)]
    [InlineData(InputDevice.Gamepad, 276U, KeyCode.GamepadA)]
    public void PressedUsesNativeWireContract(InputDevice device, uint keyCode, KeyCode expectedKey)
    {
        Verify("game.input.key.down", new
        {
            device = (int)device,
            keyCode,
            userEvent = "Action О©",
            heldSeconds = 0.0F
        }, new KeyDown(device, expectedKey, "Action О©", 0.0F));
    }

    [Theory]
    [InlineData(InputDevice.Keyboard, 65U, KeyCode.A)]
    [InlineData(InputDevice.Keyboard, 87U, KeyCode.W)]
    [InlineData(InputDevice.Keyboard, 27U, KeyCode.Escape)]
    [InlineData(InputDevice.Mouse, 263U, KeyCode.MouseButton8)]
    [InlineData(InputDevice.Gamepad, 281U, KeyCode.GamepadRightTrigger)]
    public void ReleasedUsesNativeWireContract(InputDevice device, uint keyCode, KeyCode expectedKey)
    {
        Verify("game.input.key.up", new
        {
            device = (int)device,
            keyCode,
            userEvent = "",
            heldSeconds = 2.5F
        }, new KeyUp(device, expectedKey, "", 2.5F));
    }

    [Fact]
    public void HoldUsesNativeWireContract()
    {
        Verify("game.input.key.press", new
        {
            device = (int)InputDevice.Keyboard,
            keyCode = 65U,
            userEvent = "Action О©",
            heldSeconds = 1.5F
        }, new KeyPress(InputDevice.Keyboard, KeyCode.A, "Action О©", 1.5F));
    }

    [Fact]
    public void SubscriberFailureDoesNotBlockOtherInputSubscribers()
    {
        var deliveries = 0;
        using var failing = _channel.Subscribe<KeyDown>((Action<KeyDown>)(_ =>
        {
            throw new InvalidOperationException("Input handler failed");
        }));
        using var healthy = _channel.Subscribe<KeyDown>(_ =>
        {
            deliveries++;
        });
        _channel.Publish(new KeyDown(InputDevice.Keyboard, KeyCode.A, "", 0));
        Assert.Equal(1, deliveries);
    }

    [Fact]
    public void InputSubscriptionsStopWithTheirMod()
    {
        var lifetime = new ModLifetime();
        using var events = new ScopedEvents(_channel, lifetime);
        var presses = 0;
        var holds = 0;
        var releases = 0;
        events.Subscribe<KeyDown>(_ =>
        {
            presses++;
        });
        events.Subscribe<KeyUp>(_ =>
        {
            releases++;
        });
        events.Subscribe<KeyPress>(_ =>
        {
            holds++;
        });
        var pressed = new KeyDown(InputDevice.Keyboard, KeyCode.A, "", 0);
        var released = new KeyUp(InputDevice.Keyboard, KeyCode.A, "", 1);
        var held = new KeyPress(InputDevice.Keyboard, KeyCode.A, "", 0.5F);
        _channel.Publish(pressed);
        _channel.Publish(held);
        _channel.Publish(released);
        events.Dispose();
        _channel.Publish(pressed);
        _channel.Publish(held);
        _channel.Publish(released);
        Assert.Equal(1, presses);
        Assert.Equal(1, holds);
        Assert.Equal(1, releases);
    }

    [Fact]
    public void TypedPublicationUsesCamelCaseWithoutEventMetadata()
    {
        System.Text.Json.JsonElement received = default;
        using var subscription = _channel.Subscribe<System.Text.Json.JsonElement>(KeyDown.EventName, value =>
        {
            received = value;
        });
        _channel.Publish(new KeyDown(InputDevice.Mouse, KeyCode.MouseLeft, "Fire", 0));
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
