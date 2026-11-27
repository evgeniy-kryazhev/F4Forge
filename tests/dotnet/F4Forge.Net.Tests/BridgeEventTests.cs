namespace F4Forge.Tests;

using F4Forge.Bridge;

public sealed class BridgeEventTests
{
    [Fact]
    public void RoutesByNameAndTypeAndPreservesSender()
    {
        var publisher = new object();
        var messages = new List<string>();
        using var subscription = Bridge.On<string>("Message", (sender, args) =>
        {
            Assert.Same(publisher, sender);
            messages.Add(args);
        });

        Bridge.Publish("message", publisher, "different name");
        Bridge.Publish("Message", publisher, 42);
        Bridge.Publish("Message", publisher, "received");

        Assert.Equal(["received"], messages);
    }

    [Fact]
    public void DisposingDuringDispatchTakesEffectOnNextPublication()
    {
        var calls = new List<string>();
        IDisposable? second = null;
        using var first = Bridge.On<int>("Tick", (_, _) =>
        {
            second!.Dispose();
            calls.Add("first");
        });
        using var secondSubscription = Bridge.On<int>("Tick", (_, _) => calls.Add("second"));
        second = secondSubscription;

        Bridge.Publish("Tick", null, 1);
        Bridge.Publish("Tick", null, 2);

        Assert.Equal(["first", "second", "first"], calls);
    }

    [Fact]
    public void ContinuesAfterAHandlerThrows()
    {
        var received = false;
        using var failing = Bridge.On<int>("Notification", (_, _) => throw new InvalidOperationException("Handler failed."));
        using var working = Bridge.On<int>("Notification", (_, _) => received = true);

        var failure = Assert.Throws<AggregateException>(() => Bridge.Publish("Notification", null, 1));

        Assert.IsType<InvalidOperationException>(Assert.Single(failure.InnerExceptions));
        Assert.True(received);
    }
}
