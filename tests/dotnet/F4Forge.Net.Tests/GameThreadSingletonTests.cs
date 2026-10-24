using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using F4Forge.Net.Events;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Tests;

public sealed partial class GameThreadSingletonTests : IAsyncLifetime
{
    private readonly NativeChannel _channel = new(Create());
    private readonly ModLifetime _lifetime = new();
    private readonly ConcurrentQueue<string> _queuedTasks = new();
    private readonly List<IDisposable> _registrations = [];
    private readonly ScopedChannel _messages;
    private readonly ScopedEvents _events;
    private readonly ModContext _context;
    private bool _isGameThread;
    private int _threadChecks;

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    public GameThreadSingletonTests()
    {
        _messages = new ScopedChannel(_channel, _lifetime);
        _events = new ScopedEvents(_channel, _lifetime);
        _context = new ModContext(@"C:\mods\GameThreadSingleton", _messages, _events, _channel.Serializer,
            lifetime: _lifetime);
        _registrations.Add(_channel.Register<object?, bool>("threading.is_game_thread", _ =>
        {
            _threadChecks++;
            return _isGameThread;
        }));
        _registrations.Add(_channel.Register<EnqueueRequest, bool>("threading.enqueue", request =>
        {
            _queuedTasks.Enqueue(request.TaskId);
            return true;
        }));
    }

    [Fact]
    public void AccessIsCachedPerContextAndDoesNotQueryEngine()
    {
        var scheduler = _context.GetSingleton<GameThread>();
        var otherContext = new ModContext(@"C:\mods\OtherGameThreadSingleton", _messages, _events,
            _channel.Serializer, lifetime: _lifetime);

        Assert.Same(scheduler, _context.GetSingleton<GameThread>());
        Assert.NotSame(scheduler, otherContext.GetSingleton<GameThread>());
        Assert.Equal(0, _threadChecks);
        Assert.Empty(_queuedTasks);
    }

    [Fact]
    public async Task BothOverloadsRunInlineAndPreserveValidationAndExceptions()
    {
        _isGameThread = true;
        var scheduler = _context.GetSingleton<GameThread>();
        var actionCalls = 0;
        var action = scheduler.RunAsync(() =>
        {
            actionCalls++;
        });
        var result = scheduler.RunAsync(() => 42);

        Assert.True(action.IsCompletedSuccessfully);
        Assert.True(result.IsCompletedSuccessfully);
        await action;
        Assert.Equal(42, await result);
        Assert.Equal(1, actionCalls);
        Assert.Empty(_queuedTasks);
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = scheduler.RunAsync((Action)null!);
        });
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = scheduler.RunAsync((Func<int>)null!);
        });

        var expected = new InvalidOperationException("Inline singleton scheduler failure.");
        var failure = scheduler.RunAsync<int>(() => throw expected);
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => failure));
    }

    [Fact]
    public async Task QueuedWorkExecutesOnlyForItsEventAndPropagatesFailures()
    {
        var scheduler = _context.GetSingleton<GameThread>();
        var result = scheduler.RunAsync(() => "Game result Ω");
        Assert.False(result.IsCompleted);
        Assert.True(_queuedTasks.TryDequeue(out var taskId));
        _isGameThread = true;
        _channel.Publish("threading.execute", new EnqueueRequest("unrelated-task"));
        Assert.False(result.IsCompleted);
        _channel.Publish("threading.execute", new EnqueueRequest(taskId!));
        Assert.Equal("Game result Ω", await result.WaitAsync(TimeSpan.FromSeconds(5)));

        _isGameThread = false;
        var expected = new InvalidOperationException("Queued singleton scheduler failure.");
        var failure = scheduler.RunAsync<int>(() => throw expected);
        ExecuteNextTask();
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(
            () => failure.WaitAsync(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public async Task CancellationTokenIsForwardedForBothOverloads()
    {
        var scheduler = _context.GetSingleton<GameThread>();
        using var cancellation = new CancellationTokenSource();
        var actionCalls = 0;
        var resultCalls = 0;
        var action = scheduler.RunAsync(() =>
        {
            actionCalls++;
        }, cancellation.Token);
        var result = scheduler.RunAsync(() =>
        {
            resultCalls++;
            return 42;
        }, cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(5)));
        while (!_queuedTasks.IsEmpty)
        {
            ExecuteNextTask();
        }
        Assert.Equal(0, actionCalls);
        Assert.Equal(0, resultCalls);

        var checksBeforeCall = _threadChecks;
        var alreadyCancelled = scheduler.RunAsync(() => 73, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => alreadyCancelled);
        Assert.Equal(checksBeforeCall, _threadChecks);
        Assert.Empty(_queuedTasks);
    }

    [Fact]
    public async Task ModShutdownCancelsQueuedWorkAndRejectsRetainedProxy()
    {
        var scheduler = _context.GetSingleton<GameThread>();
        var singletonWork = scheduler.RunAsync(() => 42);
        var secondSingletonWork = _context.GetSingleton<GameThread>().RunAsync(() => 73);

        _lifetime.Close();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => singletonWork.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondSingletonWork.WaitAsync(TimeSpan.FromSeconds(5)));
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = scheduler.RunAsync(() => 99);
        });
    }

    [Fact]
    public async Task FirstLookupAfterModShutdownUsesTheClosedLifetime()
    {
        _lifetime.Close();
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        var scheduler = _context.GetSingleton<GameThread>();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = scheduler.RunAsync(() => 42);
        });
        Assert.Equal(0, _threadChecks);
        Assert.Empty(_queuedTasks);
    }

    private void ExecuteNextTask()
    {
        Assert.True(_queuedTasks.TryDequeue(out var taskId));
        _isGameThread = true;
        _channel.Publish("threading.execute", new EnqueueRequest(taskId!));
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            _events.Dispose();
            _messages.Dispose();
            await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }
            Destroy();
        }
    }

    private sealed record EnqueueRequest(string TaskId);
}
