using System.Runtime.CompilerServices;
using F4Forge.DotNet.Runtime.Plugins;
using F4Forge.DotNet.Sdk;
using F4Forge.FailureFixture;
using F4Forge.PluginFixture;

namespace F4Forge.DotNet.Tests;

public sealed class PluginLoaderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void ManifestlessDiscoveryIsExplicitlyOptIn()
    {
        using var directory = new TemporaryDirectory("manifestless");
        CopyFixture(directory.Path, "Fixture.dll");

        var enabled = CreateLoader(allowManifestlessPlugins: true);
        Assert.Equal(1, enabled.LoadDirectory(directory.Path));
        Assert.True(enabled.IsActive("fixture.plugin"));

        var disabled = CreateLoader();
        Assert.Equal(0, disabled.LoadDirectory(directory.Path));
    }

    [Fact]
    public void DuplicatePluginIdIsRejected()
    {
        using var directory = new TemporaryDirectory("duplicates");
        var first = CopyFixture(directory.Path, "Fixture.dll");
        var duplicate = CopyFixture(directory.Path, "Duplicate.dll");
        var loader = CreateLoader(allowManifestlessPlugins: true);

        Assert.True(loader.Load(first));
        Assert.False(loader.Load(duplicate));
        Assert.Equal(1, loader.Count);
    }

    [Fact]
    public void PluginWhoseLoadHookThrowsIsRejected()
    {
        var loader = CreateLoader(allowManifestlessPlugins: true);

        Assert.False(loader.Load(typeof(FailurePlugin).Assembly.Location));
        Assert.Equal(0, loader.Count);
    }

    [Fact]
    public void ManifestPluginLoadsAndUnloads()
    {
        using var root = CreateManifestPlugin("valid", "fixture.plugin", typeof(FixturePlugin).Assembly.Location,
            "{\"id\":\"fixture.plugin\",\"version\":\"1.0.0\",\"runtime\":\"dotnet\",\"entryAssembly\":\"Plugin.dll\"}");
        var loader = CreateLoader();

        Assert.Equal(1, loader.LoadDirectory(root.Path));
        Assert.True(loader.IsActive("fixture.plugin"));

        loader.UnloadAll();
        Assert.Equal(0, loader.Count);
    }

    [Theory]
    [InlineData("{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"minimumF4ForgeVersion\":\"99.0.0\"}")]
    [InlineData("{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"minimumF4ForgeVersion\":\"not-a-version\"}")]
    [InlineData("{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"dependencies\":[\"missing.plugin\"]}")]
    public void InvalidOrUnsatisfiedManifestIsRejected(string manifest)
    {
        using var root = CreateManifestPlugin("invalid", "Plugin", typeof(FixturePlugin).Assembly.Location, manifest);

        Assert.Equal(0, CreateLoader().LoadDirectory(root.Path));
    }

    [Fact]
    public void DependentsDoNotActivateWhenDependencyFails()
    {
        using var root = new TemporaryDirectory("activation");
        CreateManifestDirectory(root.Path, "Failed", "Failure.dll", typeof(FailurePlugin).Assembly.Location,
            "{\"id\":\"failure.plugin\",\"entryAssembly\":\"Failure.dll\"}");
        CreateManifestDirectory(root.Path, "Dependent", "Dependent.dll", typeof(FixturePlugin).Assembly.Location,
            "{\"id\":\"dependent.plugin\",\"entryAssembly\":\"Dependent.dll\",\"dependencies\":[\"failure.plugin\"]}");
        var loader = CreateLoader();

        Assert.Equal(0, loader.LoadDirectory(root.Path));
        Assert.Equal(0, loader.Count);
        Assert.False(loader.IsActive("dependent.plugin"));
    }

    [Fact]
    public void DuplicateManifestIdsAreRejectedAsASet()
    {
        using var root = new TemporaryDirectory("manifest-duplicates");
        CreateManifestDirectory(root.Path, "One", "One.dll", typeof(FixturePlugin).Assembly.Location,
            "{\"id\":\"duplicate.plugin\",\"entryAssembly\":\"One.dll\"}");
        CreateManifestDirectory(root.Path, "Two", "Two.dll", typeof(FixturePlugin).Assembly.Location,
            "{\"id\":\"duplicate.plugin\",\"entryAssembly\":\"Two.dll\"}");

        Assert.Equal(0, CreateLoader().LoadDirectory(root.Path));
    }

    [Fact]
    public async Task UnloadQuarantinesPluginUntilActiveDispatchCompletes()
    {
        using var directory = new TemporaryDirectory("quarantine");
        var fixture = CopyFixture(directory.Path, "Fixture.dll");
        var loader = CreateLoader();
        Assert.True(loader.Load(fixture));
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatch = Task.Run(() => loader.Dispatch(_ =>
        {
            callbackEntered.TrySetResult();
            releaseCallback.Task.GetAwaiter().GetResult();
        }));
        await callbackEntered.Task.WaitAsync(Timeout);

        await Task.Run(loader.UnloadAll).WaitAsync(Timeout);

        Assert.True(loader.IsQuarantined("fixture.plugin"));
        releaseCallback.TrySetResult();
        await dispatch.WaitAsync(Timeout);
        Assert.True(SpinWait.SpinUntil(() => !loader.IsQuarantined("fixture.plugin"), Timeout));
    }

    [Fact]
    public void FailureThresholdDisablesPluginAndReloadReactivatesIt()
    {
        using var directory = new TemporaryDirectory("reload");
        CopyFixture(directory.Path, "Fixture.dll");
        var loader = CreateLoader(2, allowManifestlessPlugins: true);
        Assert.Equal(1, loader.LoadDirectory(directory.Path));

        loader.Dispatch(ThrowCallbackFailure);
        loader.Dispatch(ThrowCallbackFailure);
        Assert.False(loader.IsActive("fixture.plugin"));

        Assert.True(loader.Reload("fixture.plugin"));
        Assert.True(loader.IsActive("fixture.plugin"));
    }

    [Fact]
    public void PluginLoadContextIsCollectible()
    {
        var reference = LoadAndUnloadContext(typeof(FixturePlugin).Assembly.Location);

        for (var attempt = 0; attempt < 3 && reference.IsAlive; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(reference.IsAlive);
    }

    private static void ThrowCallbackFailure(F4ForgePlugin _) => throw new InvalidOperationException("callback failure");

    private static unsafe PluginLoader CreateLoader(int failureThreshold = 3, bool allowManifestlessPlugins = false) =>
        new(failureThreshold, allowManifestlessPlugins);

    private static string CopyFixture(string directory, string name)
    {
        var destination = System.IO.Path.Combine(directory, name);
        File.Copy(typeof(FixturePlugin).Assembly.Location, destination);
        return destination;
    }

    private static TemporaryDirectory CreateManifestPlugin(string category, string directoryName, string assembly, string manifest)
    {
        var root = new TemporaryDirectory(category);
        CreateManifestDirectory(root.Path, directoryName, "Plugin.dll", assembly, manifest);
        return root;
    }

    private static void CreateManifestDirectory(string root, string name, string assemblyName, string assembly, string manifest)
    {
        var directory = System.IO.Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        File.Copy(assembly, System.IO.Path.Combine(directory, assemblyName));
        File.WriteAllText(System.IO.Path.Combine(directory, "f4forge.plugin.json"), manifest);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadAndUnloadContext(string path)
    {
        var context = new PluginLoadContext(path);
        _ = context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(path));
        var reference = new WeakReference(context);
        context.Unload();
        return reference;
    }
}
