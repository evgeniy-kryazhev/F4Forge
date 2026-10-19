using System.Runtime.InteropServices;
using F4Forge.Net.Interop;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Tests;

public sealed partial class EventTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestErrorCount")]
    private static partial int ErrorCount();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestPublish")]
    private static partial void PublishNative();

    [Fact]
    public void NativePublicationUsesTypedSdkSubscriber()
    {
        Notification? received = null;
        using var subscription = _channel.Subscribe<Notification>("native.event", value => received = value);
        PublishNative();
        Assert.Equal("Привет 🌍", received?.Text);
    }

    [Fact]
    public void DeliversInRegistrationOrderWithoutReplayOnPublisherThread()
    {
        var received = new List<string>();
        var publisherThread = Environment.CurrentManagedThreadId;
        _channel.Publish("test.event", new Notification("Old"));
        using var first = _channel.Subscribe<Notification>("test.event", data =>
        {
            Assert.Equal(publisherThread, Environment.CurrentManagedThreadId);
            received.Add("first:" + data.Text);
        });
        using var second = _channel.Subscribe<Notification>("test.event", data => received.Add("second:" + data.Text));
        using var unrelated = _channel.Subscribe<Notification>("unrelated", _ => throw new InvalidOperationException());
        _channel.Publish("test.event", new Notification("New"));
        Assert.Equal(["first:New", "second:New"], received);
        Assert.Equal(0, ErrorCount());
    }

    [Fact]
    public void FailedSubscriberIsLoggedAndDoesNotStopDelivery()
    {
        using var broken = _channel.Subscribe<int>("test.event", (Action<int>)(_ => throw new InvalidOperationException("Broken Ω")));
        var received = 0;
        using var healthy = _channel.Subscribe<int>("test.event", value => received = value);
        _channel.Publish("test.event", 42);
        Assert.Equal(42, received);
        Assert.Equal(1, ErrorCount());
    }

    [Fact]
    public void TypeMismatchIsIsolatedLikeOtherSubscriberErrors()
    {
        using var broken = _channel.Subscribe<int>("test.event", _ => throw new InvalidOperationException("Must not execute"));
        string? received = null;
        using var healthy = _channel.Subscribe<string>("test.event", value => received = value);
        _channel.Publish("test.event", "not an integer");
        Assert.Equal("not an integer", received);
        Assert.Equal(1, ErrorCount());
    }

    [Fact]
    public void CallbackCanRemoveItselfAndLaterSubscriber()
    {
        var received = new List<int>();
        IDisposable? self = null;
        IDisposable? later = null;
        self = _channel.Subscribe<int>("test.event", value =>
        {
            self!.Dispose();
            later!.Dispose();
            _channel.Publish("test.event", value + 1);
            received.Add(value);
        });
        later = _channel.Subscribe<int>("test.event", received.Add);
        using (self)
        using (later)
        {
            _channel.Publish("test.event", 1);
            _channel.Publish("test.event", 2);
        }
        Assert.Equal([1], received);
        Assert.Equal(0, ErrorCount());
    }

    [Fact]
    public void EventsAndMessagesCanCallEachOther()
    {
        var received = new List<int>();
        using var nestedEvent = _channel.Subscribe<int>("nested", received.Add);
        using var method = _channel.Register<int, int>("managed.method", value =>
        {
            _channel.Publish("nested", value + 1);
            return value;
        });
        using var firstEvent = _channel.Subscribe<int>("first", value =>
        {
            received.Add(_channel.Call<int>("managed.method", value));
        });
        _channel.Publish("first", 8);
        Assert.Equal([9, 8], received);
    }

    [Fact]
    public async Task ConcurrentDisposersWaitForActiveDelivery()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var disposalStarted = new CountdownEvent(2);
        var subscription = _channel.Subscribe<int>("test.event", _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Delivery was not released.");
            }
        });
        var delivery = Task.Run(() => _channel.Publish("test.event", 1));
        Task[] removals = [];
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            removals = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                disposalStarted.Signal();
                subscription.Dispose();
            })).ToArray();
            Assert.True(disposalStarted.Wait(TimeSpan.FromSeconds(10)));
            await Task.Delay(100);
            Assert.All(removals, removal => Assert.False(removal.IsCompleted));
        }
        finally
        {
            release.Set();
            await Task.WhenAll(removals.Append(delivery)).WaitAsync(TimeSpan.FromSeconds(10));
            subscription.Dispose();
        }
        _channel.Publish("test.event", 2);
        Assert.Equal(0, ErrorCount());
    }

    [Fact]
    public void StoppingModClosesBothChannelsAndRemovesSubscriptions()
    {
        var lifetime = new ModLifetime();
        using var messages = new ScopedChannel(_channel, lifetime);
        using var events = new ScopedEvents(_channel, lifetime);
        var received = 0;
        events.Subscribe<int>("test.event", value => received = value);
        messages.Register<int, int>("test.method", value => value);
        events.Dispose();
        _channel.Publish("test.event", 9);
        Assert.Equal(0, received);
        Assert.Throws<ObjectDisposedException>(() => events.Publish("test.event", 1));
        Assert.Throws<ObjectDisposedException>(() => messages.Call<int>("native.echo", 1));
        Assert.Throws<ObjectDisposedException>(() => events.Subscribe<int>("test.event", _ => { }));
    }

    [Fact]
    public void OversizedEventIsRejectedBeforeDelivery()
    {
        var received = false;
        using var subscription = _channel.Subscribe<string>("test.event", _ => received = true);
        Assert.Throws<ArgumentException>(() => _channel.Publish("test.event", new string('x', 1024 * 1024)));
        Assert.False(received);
    }

    public void Dispose()
    {
        Destroy();
    }

    public sealed record Notification(string Text);
}
