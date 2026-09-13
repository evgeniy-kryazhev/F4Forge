using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Plugins;
using F4Forge.DotNet.Runtime.Plugins.Discovery;
using F4Forge.DotNet.Runtime.Scheduling;
using F4Forge.DotNet.Sdk;
using F4Forge.PluginFixture;

namespace F4Forge.DotNet.Tests;

public sealed class PluginComponentTests
{
    private static ulong _queuedTask;

    [Fact]
    public void ManifestValidationAndDiscoveryAreIndependent()
    {
        using var root = new TemporaryDirectory("components-discovery");
        var plugin = Path.Combine(root.Path, "Plugin");
        Directory.CreateDirectory(plugin);
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(plugin, "Plugin.dll"));
        var manifest = Path.Combine(plugin, "f4forge.plugin.json");
        File.WriteAllText(manifest,
            "{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"minimumF4ForgeVersion\":\"0.1.0\"}");
        var validator = new PluginManifestValidator("0.1.0");

        Assert.Equal("fixture.plugin", validator.ReadAndValidate(manifest)!.Id);
        Assert.Single(new PluginDiscovery(validator).Discover(root.Path, false).Candidates);
    }

    [Fact]
    public void DependencyResolverOrdersProviderBeforeConsumer()
    {
        var provider = new PluginCandidate("provider.dll", new PluginManifest { Id = "provider" });
        var consumer = new PluginCandidate("consumer.dll", new PluginManifest
        {
            Id = "consumer",
            Dependencies = ["provider"]
        });

        Assert.Equal([provider, consumer], new PluginDependencyResolver().Order([consumer, provider]));
    }

    [Fact]
    public unsafe void ActivatorAssemblesPluginWithPublicContextServices()
    {
        var activator = new PluginAssemblyActivator(null, null, 0, null);
        var instance = activator.Create(typeof(FixturePlugin).Assembly.Location);

        Assert.NotNull(instance);
        Assert.NotNull(instance.ContextInfo.GameThread);
        Assert.True(instance.Stop());
    }

    [Fact]
    public async Task PluginUnloadCancelsPendingGameThreadWorkBeforeContextUnload()
    {
        using var harness = new SchedulerHarness();
        var activator = harness.CreateActivator();
        var instance = activator.Create(typeof(FixturePlugin).Assembly.Location)!;
        var pending = instance.ContextInfo.GameThread.InvokeAsync(() => { });

        Assert.True(instance.Stop());
        await Assert.ThrowsAsync<TaskCanceledException>(() => pending);
        harness.Scheduler.Execute(_queuedTask);
        Assert.Equal(PluginState.Unloaded, instance.State);
    }

    [Fact]
    public unsafe void RegistryDoesNotHoldItsLockWhileDispatchingPluginCode()
    {
        var registry = new PluginInstanceRegistry();
        var dispatcher = new PluginDispatcher(registry, 3);
        var instance = new PluginAssemblyActivator(null, null, 0, null)
            .Create(typeof(FixturePlugin).Assembly.Location)!;
        Assert.True(registry.TryAdd(instance));
        Assert.True(instance.Activate());

        dispatcher.Dispatch(_ => Assert.True(Task.Run(() => registry.Count).Wait(TimeSpan.FromSeconds(1))));

        Assert.True(registry.TryRemove(instance.Id, out _));
        Assert.True(instance.Stop());
    }

    [Fact]
    public unsafe void NativeBridgeFinalizationCallbackRunsOnce()
    {
        var bridge = new NativeHostBridge(null, null, 0, "test");
        var calls = 0;
        bridge.SetFinalizationCallback(() => Interlocked.Increment(ref calls));

        Parallel.Invoke(bridge.Dispose, bridge.Dispose);

        Assert.Equal(1, calls);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int QueueTask(void* context, ulong runtime, ulong taskId)
    {
        _queuedTask = taskId;
        return (int)F4ForgeResult.Success;
    }

    private sealed unsafe class SchedulerHarness : IDisposable
    {
        private readonly NativeApi* _host;

        public SchedulerHarness()
        {
            _host = (NativeApi*)NativeMemory.Alloc((nuint)sizeof(NativeApi));
            *_host = new NativeApi { QueueTask = &QueueTask };
            Scheduler = new ManagedTaskScheduler(_host, _host, 1);
        }

        public ManagedTaskScheduler Scheduler { get; }
        public PluginAssemblyActivator CreateActivator() => new(null, null, 0, Scheduler);

        public void Dispose()
        {
            Scheduler.Dispose();
            NativeMemory.Free(_host);
        }
    }
}
