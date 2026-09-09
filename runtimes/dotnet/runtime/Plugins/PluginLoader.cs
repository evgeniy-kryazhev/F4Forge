using System.Reflection;
using F4Forge.DotNet.Sdk;
using F4Forge.DotNet.Runtime.Events;
using F4Forge.DotNet.Runtime.Events.Input;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Plugins.Discovery;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed unsafe class PluginLoader : IDisposable
{
    private const string FrameworkVersion = "0.1.0";
    private readonly object _gate = new();
    private readonly Dictionary<string, PluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PluginInstance> _quarantined = [];
    private readonly int _failureThreshold;
    private readonly bool _allowManifestlessPlugins;
    private readonly NativeApi* _host;
    private readonly ulong _runtime;
    private readonly NativeHostBridge? _eventBridge;
    private readonly NativeKeyDownEventRouter? _keyEvents;
    private readonly NativeFrameworkEventRouter? _frameworkEvents;

    public PluginLoader(int failureThreshold = 3, bool allowManifestlessPlugins = false,
        NativeApi* host = null, ulong runtime = 0)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _allowManifestlessPlugins = allowManifestlessPlugins;
        _host = host;
        _runtime = runtime;

        if (host == null) return;

        _eventBridge = new NativeHostBridge(host, runtime, "F4Forge.Managed.Input");
        _keyEvents = new NativeKeyDownEventRouter(_eventBridge, DispatchKeyDown);

        if (host->Subscribe != null)
            _frameworkEvents = new NativeFrameworkEventRouter(
                _eventBridge, DispatchGameDataReady, DispatchGameLoaded, DispatchNewGame);
    }

    public int LoadDirectory(string directory)
    {
        var discovery = PluginDiscovery.Discover(directory, _allowManifestlessPlugins, FrameworkVersion);
        var loaded = 0;
        foreach (var candidate in PluginDependencyResolver.Order(discovery.Candidates))
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

    private bool DependenciesActive(PluginCandidate candidate, IReadOnlyList<PluginCandidate> candidates)
    {
        foreach (var dependency in candidate.Manifest.Dependencies ?? [])
        {
            var provider = candidates.FirstOrDefault(item =>
                string.Equals(item.Manifest.Id, dependency, StringComparison.OrdinalIgnoreCase) ||
                (item.Manifest.ProvidedCapabilities ?? [])
                    .Any(capability => string.Equals(capability, dependency, StringComparison.OrdinalIgnoreCase)));
            if (provider == null || !IsActive(provider.Manifest.Id!)) return false;
        }
        return true;
    }

    private bool Load(string path, string? expectedId = null)
    {
        PluginInstance? instance = null;
        try
        {
            var context = new PluginLoadContext(path);
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
            var pluginType = FindPluginType(assembly, expectedId);
            if (pluginType == null)
            {
                context.Unload();
                return false;
            }

            var plugin = (F4ForgePlugin)Activator.CreateInstance(pluginType)!;
            var pluginId = plugin.Id;
            if (string.IsNullOrWhiteSpace(pluginId))
                throw new InvalidOperationException("Plugin ID must not be empty.");
            if (expectedId != null && !pluginId.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Manifest ID '{expectedId}' does not match plugin ID '{pluginId}'.");
            instance = new PluginInstance(Path.GetFullPath(path), context, plugin, pluginId,
                new PluginResourceScope(), _host, _runtime);
            lock (_gate)
            {
                if (_plugins.ContainsKey(pluginId))
                {
                    Logger.Error($"Duplicate managed plugin id: {pluginId}");
                    instance.Stop();
                    return false;
                }
                _plugins.Add(pluginId, instance);
            }
            plugin.OnLoad(instance.ContextInfo);
            if (!instance.Activate())
            {
                instance.Stop();
                return false;
            }
            Logger.Info($"Managed plugin loaded: {pluginId}");
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error($"Managed plugin failed: {path}: {exception}");
            if (instance != null)
            {
                lock (_gate)
                {
                    if (_plugins.TryGetValue(instance.Id, out var current) && ReferenceEquals(current, instance))
                        _plugins.Remove(instance.Id);
                }
                instance.Stop();
            }
            return false;
        }
    }

    public bool Reload(string id)
    {
        PluginInstance? old;
        lock (_gate)
        {
            if (!_plugins.TryGetValue(id, out old) ||
                old.State is not (PluginState.Active or PluginState.Disabled)) return false;
            _plugins.Remove(id);
        }
        var path = old.Path;
        if (!old.Stop())
        {
            lock (_gate)
                if (!_quarantined.Contains(old)) _quarantined.Add(old);
            return false;
        }
        return Load(path);
    }

    public void Dispatch(Action<F4ForgePlugin> callback)
    {
        PluginInstance[] snapshot;
        lock (_gate) snapshot = _plugins.Values.ToArray();
        foreach (var instance in snapshot)
        {
            if (!instance.TryAcquireDispatchLease(out var lease)) continue;
            using (lease)
            try
            {
                ++PluginInstance.DispatchDepth.Value;
                callback(instance.Plugin);
                instance.ResetFailures();
            }
            catch (Exception exception)
            {
                var failures = instance.RecordFailure(_failureThreshold);
                Logger.Error($"Managed plugin callback failed: plugin={instance.Id}, " +
                    $"exception={exception.GetType().FullName}, failures={failures}, " +
                    $"threshold={_failureThreshold}: {exception}");
                if (failures >= _failureThreshold)
                    Logger.Warning($"Managed plugin disabled: plugin={instance.Id}, threshold={_failureThreshold}.");
            }
            finally { --PluginInstance.DispatchDepth.Value; }
        }
    }

    private void DispatchKeyDown(KeyDownEventArgs args)
    {
        PluginInstance[] snapshot;
        lock (_gate) snapshot = _plugins.Values.ToArray();
        foreach (var instance in snapshot)
        {
            if (!instance.TryAcquireDispatchLease(out var lease)) continue;
            using (lease) instance.ContextInfo.Events.PublishKeyDown(args);
        }
    }

    public void UnloadAll()
    {
        PluginInstance[] snapshot;
        lock (_gate)
        {
            snapshot = _plugins.Values.ToArray();
            _plugins.Clear();
        }
        foreach (var instance in snapshot)
            if (!instance.Stop())
            {
                lock (_gate)
                    if (!_quarantined.Contains(instance)) _quarantined.Add(instance);
            }
    }

    private void DispatchGameDataReady() => DispatchLifecycle(plugin => plugin.OnGameDataReady());
    private void DispatchGameLoaded() => DispatchLifecycle(plugin => plugin.OnGameLoaded());
    private void DispatchNewGame() => DispatchLifecycle(plugin => plugin.OnNewGame());

    private void DispatchLifecycle(Action<F4ForgePlugin> callback)
    {
        PluginInstance[] snapshot;
        lock (_gate) snapshot = _plugins.Values.ToArray();
        foreach (var instance in snapshot)
        {
            if (!instance.TryAcquireDispatchLease(out var lease)) continue;
            using (lease)
            {
                try { callback(instance.Plugin); }
                catch (Exception exception)
                {
                    Logger.Error($"Managed lifecycle callback failed: plugin={instance.Id}: {exception}");
                }
            }
        }
    }

    public void Dispose()
    {
        UnloadAll();
        _keyEvents?.Dispose();
        _frameworkEvents?.Dispose();
        _eventBridge?.Dispose();
    }

    public int Count { get { lock (_gate) return _plugins.Count; } }

    private bool IsActive(string id)
    {
        lock (_gate)
            return _plugins.TryGetValue(id, out var instance) && instance.State == PluginState.Active;
    }

    public bool IsQuarantined(string id)
    {
        lock (_gate) return _quarantined.Any(instance =>
            instance.Id.Equals(id, StringComparison.OrdinalIgnoreCase) &&
            instance.State == PluginState.Quarantined);
    }

    private static Type? FindPluginType(Assembly assembly, string? expectedId = null)
    {
        try { return FindPluginType(assembly.GetTypes(), expectedId); }
        catch (ReflectionTypeLoadException exception) { return FindPluginType(exception.Types.OfType<Type>(), expectedId); }
    }

    private static Type? FindPluginType(IEnumerable<Type> types, string? expectedId)
    {
        var candidates = types.Where(type => type is { IsAbstract: false, IsPublic: true } &&
                typeof(F4ForgePlugin).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        if (expectedId == null) return candidates.FirstOrDefault();
        foreach (var type in candidates)
            if (Activator.CreateInstance(type) is F4ForgePlugin plugin &&
                plugin.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase)) return type;
        return null;
    }
}
