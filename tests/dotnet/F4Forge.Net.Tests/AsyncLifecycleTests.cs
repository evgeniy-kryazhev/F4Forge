using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Callbacks;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Events;
using F4Forge.Net.Hosting;
using F4Forge.Net.Interception;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Saves.Events;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Tests;

public sealed partial class AsyncLifecycleTests : IDisposable
{
    private readonly NativeChannel _channel = new(Create());
    private readonly ModLifetime _lifetime = new();
    private readonly List<IDisposable> _nativeRegistrations = [];
    private readonly ConcurrentQueue<DiagnosticRecord> _records = new();
    private readonly ConcurrentQueue<string> _errors = new();
    private readonly ScopedChannel _messages;
    private readonly ScopedEvents _events;
    private readonly ModContext _context;

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    public AsyncLifecycleTests()
    {
        _messages = new ScopedChannel(_channel, _lifetime);
        _events = new ScopedEvents(_channel, _lifetime, _records.Enqueue, _errors.Enqueue);
        _context = new ModContext(Path.GetTempPath(), _messages, _events, _channel.Serializer, "AsyncExample.dll", _lifetime);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task CancellationCallbacksRunOutsideTheStoppingThreadAndAreDrained()
    {
        var entered = Signal();
        var release = Signal();
        using var registration = _lifetime.Token.Register(() =>
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
        });
        try
        {
            await Task.Run(_lifetime.Close).WaitAsync(TimeSpan.FromSeconds(5));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(_lifetime.Completion.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task FailedOwnedCleanupDoesNotPreventOtherCleanupOrLifetimeCompletion()
    {
        var cleaned = false;
        _lifetime.Own(new CleanupAction(() => cleaned = true));
        _lifetime.Own(new CleanupAction(() => throw new InvalidOperationException("Cleanup failed")));
        Assert.Throws<AggregateException>(_lifetime.Close);
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(cleaned);
    }

    [Fact]
    public void ScopeStillRemovesNativeCallbacksWhenLifetimeCleanupFails()
    {
        _context.Messages.Register<int, int>("cleanup.safe", value => value);
        _lifetime.Own(new CleanupAction(() => throw new InvalidOperationException("Owned cleanup failed")));
        Assert.Throws<AggregateException>(_messages.Dispose);
        Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => _channel.Call<int>("cleanup.safe", 1)).Code);
    }

    [Fact]
    public async Task RegistrationFinishingAfterScopeShutdownIsRemovedBeforeItCanEscape()
    {
        var entered = Signal();
        var release = Signal();
        var lifetime = new ModLifetime();
        using var messages = new ScopedChannel(new DelayedRegistrationChannel(_channel, entered, release), lifetime);
        var registering = Task.Run(() => messages.Register<int, int>("scope.race", value => value));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            messages.Dispose();
            Assert.False(lifetime.Completion.IsCompleted);
            release.SetResult();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                using var unexpected = await registering;
            });
            Assert.Equal("method_not_found", Assert.Throws<MessageException>(() => _channel.Call<int>("scope.race", 1)).Code);
            await lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
            try
            {
                (await registering).Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private sealed class DelayedRegistrationChannel(IMessageChannel channel, TaskCompletionSource entered,
        TaskCompletionSource release) : IMessageChannel
    {
        public TResult Call<TResult>(string method, object? arguments)
        {
            return channel.Call<TResult>(method, arguments);
        }

        public IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler)
        {
            var registration = channel.Register(method, handler);
            entered.TrySetResult();
            // Pause after native registration but before the scope can own it.
            release.Task.GetAwaiter().GetResult();
            return registration;
        }
    }

    private sealed class CleanupAction(Action cleanup) : IDisposable
    {
        public void Dispose()
        {
            cleanup();
        }
    }

    [Fact]
    public async Task AsyncLambdaUsesTaskOverloadAndPublicationDoesNotWait()
    {
        var release = Signal();
        var finished = Signal();
        var calls = new List<int>();
        using var subscription = _context.Subscribe<LifecycleNotificationEvent>(async notification =>
        {
            calls.Add(notification.Value);
            await release.Task;
            finished.TrySetResult();
            throw new InvalidOperationException("Failure after await");
        });
        _channel.Publish(new LifecycleNotificationEvent(1));
        _channel.Publish(new LifecycleNotificationEvent(2));
        Assert.Equal([1, 2], calls);
        Assert.False(finished.Task.IsCompleted);
        release.SetResult();
        _lifetime.Close();
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, _records.Count(record => record.Kind == "event.handle.async" && !record.Success));
        Assert.All(_records, record => Assert.EndsWith("AsyncExample.dll", record.ModId));
    }

    private sealed record LifecycleNotificationEvent(int Value) : IGameEvent
    {
        public static string EventName => "async";
    }

    [Fact]
    public async Task TypedAsyncSubscriptionCancelsWithoutWaitingForItsContinuation()
    {
        var release = Signal();
        CancellationToken observed = default;
        var subscription = _context.Subscribe<SaveLoadedEvent>(async (_, token) =>
        {
            observed = token;
            await release.Task;
        });
        _channel.Publish("game.save.loaded", new SaveLoadedEvent("Example", true));
        subscription.Dispose();
        Assert.True(observed.IsCancellationRequested);
        _lifetime.Close();
        Assert.False(_lifetime.Completion.IsCompleted);
        release.SetResult();
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task HandlerCanStopItsOwnLifetimeWithoutDeadlocking()
    {
        var continued = Signal();
        using var subscription = _context.Subscribe<int>("stop", async (_, token) =>
        {
            _events.Dispose();
            await Task.Yield();
            Assert.True(token.IsCancellationRequested);
            continued.SetResult();
        });
        _channel.Publish("stop", 0);
        await continued.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<ObjectDisposedException>(() => _context.Messages.Call<int>("native.echo", 1));
    }

    [Fact]
    public async Task GameThreadRunsInlineOrQueuesAndPropagatesFailuresAndCancellation()
    {
        var gameThread = Environment.CurrentManagedThreadId;
        var queued = new ConcurrentQueue<string>();
        _nativeRegistrations.Add(_channel.Register<object?, bool>("threading.is_game_thread", _ =>
            Environment.CurrentManagedThreadId == Volatile.Read(ref gameThread)));
        _nativeRegistrations.Add(_channel.Register<JsonElement, bool>("threading.enqueue", input =>
        {
            queued.Enqueue(input.GetProperty("taskId").GetString()!);
            return true;
        }));
        var immediate = _context.GetSingleton<GameThread>().RunAsync(() => 42);
        Assert.True(immediate.IsCompletedSuccessfully);
        Assert.Equal(42, await immediate);
        Volatile.Write(ref gameThread, 0);
        var result = _context.GetSingleton<GameThread>().RunAsync(() => 73);
        Assert.False(result.IsCompleted);
        Assert.True(queued.TryDequeue(out var id));
        Volatile.Write(ref gameThread, Environment.CurrentManagedThreadId);
        _channel.Publish("threading.execute", new { TaskId = id });
        Assert.Equal(73, await result.WaitAsync(TimeSpan.FromSeconds(5)));
        Volatile.Write(ref gameThread, 0);
        using var cancellation = new CancellationTokenSource();
        var executed = false;
        var cancelled = _context.GetSingleton<GameThread>().RunAsync(() => executed = true, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.True(queued.TryDequeue(out var cancelledId));
        _channel.Publish("threading.execute", new { TaskId = cancelledId });
        Assert.False(executed);
        var failure = _context.GetSingleton<GameThread>().RunAsync<int>(() => throw new InvalidOperationException("Game-thread failure"));
        Assert.True(queued.TryDequeue(out var failedId));
        Volatile.Write(ref gameThread, Environment.CurrentManagedThreadId);
        _channel.Publish("threading.execute", new { TaskId = failedId });
        await Assert.ThrowsAsync<InvalidOperationException>(() => failure);
        Volatile.Write(ref gameThread, 0);
        var stopped = _context.GetSingleton<GameThread>().RunAsync(() => 1);
        _lifetime.Close();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopped);
    }

    [Fact]
    public void InterceptorsCommitInOrderRollbackFailureAndStopAtSkip()
    {
        ConsoleCommandInterception? captured = null;
        using var first = _context.GetSingleton<GameInterception>().OnConsoleCommandExecuting(context =>
        {
            Assert.Equal("help", context.OriginalCommand);
            context.Command = "modified";
            captured = context;
        });
        using var broken = _context.GetSingleton<GameInterception>().OnConsoleCommandExecuting(context =>
        {
            context.Command = "must be rolled back";
            throw new InvalidOperationException("Interceptor failed");
        });
        using var skip = _context.GetSingleton<GameInterception>().OnConsoleCommandExecuting(context =>
        {
            Assert.Equal("modified", context.Command);
            context.Skip = true;
        });
        using var unreachable = _context.GetSingleton<GameInterception>().OnConsoleCommandExecuting(_ => Assert.Fail("Skip did not terminate the chain"));
        var result = _channel.Call<JsonElement>("test.intercept",
            new { OriginalCommand = "help", Command = "help", SelectedReferenceFormId = 0x14, Skip = false });
        Assert.Equal("modified", result.GetProperty("command").GetString());
        Assert.True(result.GetProperty("skip").GetBoolean());
        Assert.Throws<InvalidOperationException>(() => captured!.Command = "late");
        Assert.Throws<InvalidOperationException>(() => captured!.Skip = true);
    }

    [Fact]
    public void SharedInterceptorAdapterCompletesCustomContextsWhenDecisionConversionFails()
    {
        CustomInterception? captured = null;
        using var interceptor = _context.GetSingleton<GameInterception>().Register<CustomInput, CustomInterception, object>(
            "console.command.executing", context => captured = context,
            input => new CustomInterception(input.Command),
            _ => throw new InvalidOperationException("Decision conversion failed"));
        var result = _channel.Call<JsonElement>("test.intercept",
            new { OriginalCommand = "help", Command = "help", SelectedReferenceFormId = 0x14, Skip = false });
        Assert.Equal("help", result.GetProperty("command").GetString());
        Assert.Throws<InvalidOperationException>(() => captured!.Command = "after return");
    }

    private sealed record CustomInput(string Command);

    private sealed class CustomInterception(string command) : InvocationContext
    {
        private string _command = command;

        internal string Command
        {
            get => _command;
            set
            {
                EnsureActive();
                _command = value;
            }
        }
    }

    [Fact]
    public async Task DetachingTransportPreventsLateAsyncNativeAccess()
    {
        var release = Signal();
        var attempted = Signal();
        using var subscription = _context.Subscribe<int>("late", async _ =>
        {
            await release.Task;
            Assert.Throws<ObjectDisposedException>(() => _context.Messages.Call<int>("native.echo", 1));
            attempted.SetResult();
        });
        _channel.Publish("late", 1);
        _events.Dispose();
        _messages.Dispose();
        _channel.DetachTransport();
        Destroy();
        release.SetResult();
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PendingStartupDoesNotPreventAnotherModFromStarting()
    {
        var root = Path.Combine(Path.GetTempPath(), "F4Forge.Async.Tests", Guid.NewGuid().ToString("N"));
        var waiting = Deploy(root, "A-Waiting", asynchronous: true);
        Deploy(root, "B-Ready", asynchronous: false);
        var errors = new ConcurrentQueue<string>();
        var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Enqueue);
        try
        {
            loader.LoadDirectory(root);
            Assert.Equal(1, loader.Count);
            Assert.Equal(waiting, _channel.Call<string>("fixture.starting", null));
            Assert.EndsWith("B-Ready", _channel.Call<string>("fixture.directory", null));
            File.WriteAllText(Path.Combine(waiting, "release-start"), string.Empty);
            await WaitUntilAsync(() => loader.Count == 2);
            Assert.Empty(errors);
        }
        finally
        {
            await loader.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SlowShutdownWarnsAndRetainsTheModUntilItActuallyFinishes()
    {
        var root = Path.Combine(Path.GetTempPath(), "F4Forge.Async.Tests", Guid.NewGuid().ToString("N"));
        var directory = Deploy(root, "Slow", asynchronous: true);
        File.WriteAllText(Path.Combine(directory, "ignore-cancellation"), string.Empty);
        var warning = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new ModLoader(_channel, _channel, _channel.Serializer, text => warning.TrySetResult(text));
        try
        {
            loader.LoadDirectory(root);
            loader.Dispose();
            var message = await warning.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Contains("five seconds", message);
            Assert.False(loader.Completion.IsCompleted);
            Assert.False(File.Exists(Path.Combine(directory, "disposed")));
        }
        finally
        {
            File.WriteAllText(Path.Combine(directory, "release-start"), string.Empty);
            await loader.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FailureAfterAwaitRollsBackRegistrationsAndDisposesOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "F4Forge.Async.Tests", Guid.NewGuid().ToString("N"));
        var directory = Deploy(root, "Failing", asynchronous: true);
        File.WriteAllText(Path.Combine(directory, "fail-after-await"), string.Empty);
        var errors = new ConcurrentQueue<string>();
        var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Enqueue);
        try
        {
            loader.LoadDirectory(root);
            File.WriteAllText(Path.Combine(directory, "release-start"), string.Empty);
            await WaitUntilAsync(() => !errors.IsEmpty);
            await loader.DisposeAsync();
            Assert.Equal(0, loader.Count);
            Assert.Throws<MessageException>(() => _channel.Call<string>("fixture.starting", null));
            Assert.Equal("disposed\n", File.ReadAllText(Path.Combine(directory, "disposed")));
        }
        finally
        {
            await loader.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopRevokesNativeAccessAndRetainsIncompleteStartup(bool ignoreCancellation)
    {
        var root = Path.Combine(Path.GetTempPath(), "F4Forge.Async.Tests", Guid.NewGuid().ToString("N"));
        var directory = Deploy(root, "Pending", asynchronous: true);
        if (ignoreCancellation)
        {
            File.WriteAllText(Path.Combine(directory, "ignore-cancellation"), string.Empty);
        }
        var errors = new ConcurrentQueue<string>();
        var loader = new ModLoader(_channel, _channel, _channel.Serializer, errors.Enqueue);
        try
        {
            loader.LoadDirectory(root);
            loader.Dispose();
            Assert.Throws<MessageException>(() => _channel.Call<string>("fixture.starting", null));
            if (ignoreCancellation)
            {
                Assert.False(loader.Completion.IsCompleted);
                Assert.False(File.Exists(Path.Combine(directory, "disposed")));
                File.WriteAllText(Path.Combine(directory, "release-start"), string.Empty);
            }
            await loader.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await loader.DisposeAsync();
            Assert.Equal("disposed\n", File.ReadAllText(Path.Combine(directory, "disposed")));
            Assert.Empty(errors);
        }
        finally
        {
            File.WriteAllText(Path.Combine(directory, "release-start"), string.Empty);
            await loader.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Deploy(string root, string name, bool asynchronous)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        foreach (var assembly in new[] { "Fixture.dll", "FixtureDependency.dll" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, assembly), Path.Combine(directory, assembly));
        }
        if (asynchronous)
        {
            File.WriteAllText(Path.Combine(directory, "async-start"), string.Empty);
        }
        return directory;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    public void Dispose()
    {
        _events.Dispose();
        _messages.Dispose();
        foreach (var registration in _nativeRegistrations.AsEnumerable().Reverse())
        {
            registration.Dispose();
        }
        Destroy();
    }
}
