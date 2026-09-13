using F4Forge.DotNet.Runtime.Events;
using F4Forge.DotNet.Runtime.Events.Input;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Plugins.Discovery;
using F4Forge.DotNet.Runtime.Scheduling;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed unsafe class PluginLoader : IDisposable
{
    private const string FrameworkVersion = "0.1.0";
    private readonly bool _allowManifestlessPlugins;
    private readonly PluginDiscovery _discovery;
    private readonly PluginDependencyResolver _dependencies;
    private readonly PluginAssemblyActivator _activator;
    private readonly PluginInstanceRegistry _registry;
    private readonly PluginDispatcher _dispatcher;
    private readonly PluginUnloader _unloader;
    private readonly NativeHostBridge? _eventBridge;
    private readonly NativeKeyDownEventRouter? _keyEvents;
    private readonly NativeFrameworkEventRouter? _frameworkEvents;
    private readonly ManagedTaskScheduler? _taskScheduler;

    public PluginLoader(int failureThreshold = 3, bool allowManifestlessPlugins = false,
        NativeApi* host = null, void* hostContext = null, ulong runtime = 0)
    {
        _allowManifestlessPlugins = allowManifestlessPlugins;
        _registry = new PluginInstanceRegistry();
        _discovery = new PluginDiscovery(new PluginManifestValidator(FrameworkVersion));
        _dependencies = new PluginDependencyResolver();
        _taskScheduler = host == null ? null : new ManagedTaskScheduler(host, hostContext, runtime);
        _activator = new PluginAssemblyActivator(host, hostContext, runtime, _taskScheduler);
        _dispatcher = new PluginDispatcher(_registry, Math.Max(1, failureThreshold));
        _unloader = new PluginUnloader(_registry);
        if (host == null) return;
        _eventBridge = new NativeHostBridge(host, hostContext, runtime, "F4Forge.Managed.Input");
        _keyEvents = new NativeKeyDownEventRouter(_eventBridge, _dispatcher.DispatchInput);
        if (host->Subscribe != null)
            _frameworkEvents = new NativeFrameworkEventRouter(_eventBridge,
                () => _dispatcher.DispatchLifecycle(events => events.PublishGameDataReady()),
                () => _dispatcher.DispatchLifecycle(events => events.PublishGameLoaded()),
                () => _dispatcher.DispatchLifecycle(events => events.PublishNewGame()));
    }

    internal PluginLoader(bool allowManifestlessPlugins, PluginDiscovery discovery,
        PluginDependencyResolver dependencies, PluginAssemblyActivator activator,
        PluginInstanceRegistry registry, PluginDispatcher dispatcher, PluginUnloader unloader)
    {
        _allowManifestlessPlugins = allowManifestlessPlugins;
        _discovery = discovery;
        _dependencies = dependencies;
        _activator = activator;
        _registry = registry;
        _dispatcher = dispatcher;
        _unloader = unloader;
    }

    public int LoadDirectory(string directory)
    {
        var discovery = _discovery.Discover(directory, _allowManifestlessPlugins);
        var loaded = 0;
        foreach (var candidate in _dependencies.Order(discovery.Candidates))
        {
            if (!DependenciesActive(candidate, discovery.Candidates))
            {
                Logger.Error($"Managed plugin '{candidate.Manifest.Id}' skipped because a dependency is inactive.");
                continue;
            }
            if (Load(candidate.Path, candidate.Manifest.Id)) ++loaded;
        }
        foreach (var path in discovery.LegacyPaths)
            if (Load(path)) ++loaded;
        return loaded;
    }

    internal bool Load(string path, string? expectedId = null)
    {
        PluginInstance? instance = null;
        try
        {
            instance = _activator.Create(path, expectedId);
            if (instance == null) return false;
            if (!_registry.TryAdd(instance))
            {
                Logger.Error($"Duplicate managed plugin id: {instance.Id}");
                _unloader.Stop(instance);
                return false;
            }
            instance.Plugin.OnLoad(instance.ContextInfo);
            if (!instance.Activate()) throw new InvalidOperationException("Plugin could not be activated.");
            Logger.Info($"Managed plugin loaded: {instance.Id}");
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error($"Managed plugin failed: {path}: {exception}");
            if (instance != null)
            {
                _registry.Remove(instance);
                _unloader.Stop(instance);
            }
            return false;
        }
    }

    public bool Reload(string id)
    {
        if (!_registry.TryRemove(id, out var old) || old == null ||
            old.State is not (PluginState.Active or PluginState.Disabled)) return false;
        var path = old.Path;
        return _unloader.Stop(old) && Load(path);
    }

    public void Dispatch(Action<F4ForgePlugin> callback) => _dispatcher.Dispatch(callback);
    public void UnloadAll() => _unloader.StopAll();
    internal void ExecuteTask(ulong taskId) => _taskScheduler?.Execute(taskId);
    public int Count => _registry.Count;
    internal bool IsActive(string id) => _registry.IsActive(id);
    public bool IsQuarantined(string id) => _registry.IsQuarantined(id);

    public void Dispose()
    {
        UnloadAll();
        _keyEvents?.Dispose();
        _frameworkEvents?.Dispose();
        _eventBridge?.Dispose();
        _taskScheduler?.Dispose();
    }

    private bool DependenciesActive(PluginCandidate candidate, IReadOnlyList<PluginCandidate> candidates)
    {
        foreach (var dependency in candidate.Manifest.Dependencies ?? [])
        {
            var provider = candidates.FirstOrDefault(item =>
                string.Equals(item.Manifest.Id, dependency, StringComparison.OrdinalIgnoreCase) ||
                (item.Manifest.ProvidedCapabilities ?? []).Any(capability =>
                    string.Equals(capability, dependency, StringComparison.OrdinalIgnoreCase)));
            if (provider == null || !_registry.IsActive(provider.Manifest.Id!)) return false;
        }
        return true;
    }
}
