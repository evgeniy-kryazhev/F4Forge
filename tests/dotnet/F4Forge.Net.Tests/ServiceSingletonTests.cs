using System.Runtime.InteropServices;
using System.Text.Json;
using F4Forge.Net.Console;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Events;
using F4Forge.Net.Interception;
using F4Forge.Net.Interop;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Papyrus;
using static F4Forge.Net.Singletons.SingletonExtensions;

namespace F4Forge.Net.Tests;

public sealed partial class ServiceSingletonTests : IAsyncLifetime
{
    private readonly NativeChannel _channel = new(Create());
    private readonly List<ModLifetime> _lifetimes = [];

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestCreate")]
    private static partial ChannelBinding Create();

    [LibraryImport("F4Forge.TestTransport", EntryPoint = "TestDestroy")]
    private static partial void Destroy();

    [Fact]
    public void ServicesAreCachedPerContextWithoutNativeCalls()
    {
        var first = CreateContext("First.dll");
        var second = CreateContext("Second.dll");

        AssertContextOwnership<GameConsole>(first, second);
        AssertContextOwnership<GameDiagnostics>(first, second);
        AssertContextOwnership<GameObjects>(first, second);
        AssertContextOwnership<GameInterception>(first, second);
        AssertContextOwnership<GamePapyrus>(first, second);
        Assert.Equal(Path.GetFullPath(@"C:\mods\Services\First.dll"), first.GetSingleton<GameDiagnostics>().ModId);
        Assert.Equal(Path.GetFullPath(@"C:\mods\Services\Second.dll"), second.GetSingleton<GameDiagnostics>().ModId);
        Assert.IsAssignableFrom<IConsole>(first.GetSingleton<GameConsole>());
    }

    [Fact]
    public async Task ConcurrentFirstLookupExecutesTheServiceFactoryOnce()
    {
        var context = CreateContext("Concurrent.dll");
        Interlocked.Exchange(ref CreationProbe.CreationCount, 0);
        using var start = new ManualResetEventSlim(false);
        var workers = Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
        {
            Assert.True(start.Wait(TimeSpan.FromSeconds(5)));
            return context.GetSingleton<CreationProbe>();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        start.Set();
        var instances = await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, CreationProbe.CreationCount);
        foreach (var instance in instances)
        {
            Assert.Same(instances[0], instance);
        }
    }

    [Fact]
    public async Task PapyrusUsesRawCleanupTransportAfterModScopeCloses()
    {
        var context = CreateContext("Cleanup.dll");
        var releases = 0;
        string? requestScope = null;
        using var begin = _channel.Register<JsonElement, bool>("papyrus.begin", request =>
        {
            requestScope = request.GetProperty("scope").GetString();
            return true;
        });
        using var release = _channel.Register<JsonElement, bool>("papyrus.release", request =>
        {
            Assert.Equal(requestScope, request.GetProperty("scope").GetString());
            Assert.Throws<ObjectDisposedException>(() => context.Messages.Call<bool>("probe.closed", null));
            releases++;
            return true;
        });
        var client = context.GetSingleton<GamePapyrus>();
        var pending = client.CallStaticAsync("Probe", "Wait");
        Assert.NotNull(requestScope);

        context.Lifetime.Close();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        await context.Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, releases);
        Assert.Same(client, context.GetSingleton<GamePapyrus>());
    }

    [Fact]
    public async Task FirstServiceLookupAfterShutdownCannotCreateAnIndependentScope()
    {
        var context = CreateContext("Closed.dll");
        context.Lifetime.Close();
        await context.Lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Throws<ObjectDisposedException>(() => context.GetSingleton<GamePapyrus>());
        Assert.Throws<ObjectDisposedException>(() => context.GetSingleton<GameConsole>().AddHandler("closed", _ => { }));
        Assert.Throws<ObjectDisposedException>(() => context.GetSingleton<GameDiagnostics>().GetHooks());
        Assert.Throws<ObjectDisposedException>(() => context.GetSingleton<GameObjects>().Query(new FormById<Form>(1)));
        Assert.Throws<ObjectDisposedException>(() => context.GetSingleton<GameInterception>().Register<int, int>("closed", value => value));
    }

    private ModContext CreateContext(string modName)
    {
        var lifetime = new ModLifetime();
        _lifetimes.Add(lifetime);
        var messages = new ScopedChannel(_channel, lifetime);
        var events = new ScopedEvents(_channel, lifetime);
        return new ModContext(@"C:\mods\Services", messages, events, _channel.Serializer,
            Path.Combine(@"C:\mods\Services", modName), lifetime);
    }

    private static void AssertContextOwnership<T>(ModContext first, ModContext second)
        where T : class, F4Forge.Net.Singletons.IGameSingleton<T>
    {
        var service = first.GetSingleton<T>();
        Assert.Same(service, first.GetSingleton<T>());
        Assert.NotSame(service, second.GetSingleton<T>());
    }

    private sealed class CreationProbe : F4Forge.Net.Singletons.IGameSingleton<CreationProbe>
    {
        public static int CreationCount;

        public static CreationProbe Create(ModContext context)
        {
            Interlocked.Increment(ref CreationCount);
            // Widen the overlap between concurrent first lookups without blocking on another factory.
            Thread.SpinWait(100_000);
            return new CreationProbe();
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            foreach (var lifetime in _lifetimes)
            {
                lifetime.Close();
                await lifetime.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            Destroy();
        }
    }
}
