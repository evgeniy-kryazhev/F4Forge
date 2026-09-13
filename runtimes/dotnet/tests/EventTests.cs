using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed class EventTests
{
    [Fact]
    public void KeyDownInvokesRemainingHandlersWhenOneThrows()
    {
        var input = new InputEvents("test.plugin");
        var calls = new List<string>();
        input.KeyDown += (_, _) => calls.Add("first");
        input.KeyDown += (_, _) => throw new InvalidOperationException("failure");
        input.KeyDown += (_, _) => calls.Add("last");

        input.PublishKeyDown(CreateKeyArgs(Key.A));

        Assert.Equal(["first", "last"], calls);
    }

    [Fact]
    public void KeyDownSupportsUnsubscribe()
    {
        var input = new InputEvents("test.plugin");
        var calls = 0;
        EventHandler<KeyDownEventArgs> handler = (_, _) => ++calls;
        input.KeyDown += handler;
        input.PublishKeyDown(CreateKeyArgs(Key.A));

        input.KeyDown -= handler;
        input.PublishKeyDown(CreateKeyArgs(Key.B));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void InputHandlersAreIsolatedByPluginInstance()
    {
        var first = new InputEvents("first.plugin");
        var second = new InputEvents("second.plugin");
        var secondCalls = 0;
        second.KeyDown += (_, _) => ++secondCalls;

        first.PublishKeyDown(CreateKeyArgs(Key.A));
        Assert.Equal(0, secondCalls);

        second.PublishKeyDown(CreateKeyArgs(Key.B));
        Assert.Equal(1, secondCalls);
    }

    [Fact]
    public void LifecycleEventsAreIsolatedFromHandlerFailures()
    {
        var events = new PluginEvents("test.plugin");
        var calls = 0;
        events.GameDataReady += (_, _) => ++calls;
        events.GameLoaded += (_, _) => throw new InvalidOperationException("failure");
        events.GameLoaded += (_, _) => ++calls;
        events.NewGame += (_, _) => ++calls;

        events.PublishGameDataReady();
        events.PublishGameLoaded();
        events.PublishNewGame();

        Assert.Equal(3, calls);
    }

    private static KeyDownEventArgs CreateKeyArgs(Key key) =>
        new(InputDevice.Keyboard, key, false, 0, false);
}
