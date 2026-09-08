using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

internal enum PluginState
{
    Loading,
    Active,
    Quiescing,
    Disabled,
    Unloaded
}

internal sealed class PluginLoader
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _failureThreshold;

    public PluginLoader(int failureThreshold = 3)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
    }

    public int LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Logger.Warning($"Managed plugin directory does not exist: {directory}");
            return 0;
        }
        var loaded = 0;

        foreach (var pluginDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(pluginDirectory, "f4forge.plugin.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                {
                    Logger.Error($"Invalid plugin manifest: {manifestPath}");
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(manifest.Runtime) &&
                    !manifest.Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    continue;
                var assemblyPath = Path.Combine(pluginDirectory, manifest.EntryAssembly);
                if (Load(assemblyPath, manifest.Id)) ++loaded;
            }
            catch (Exception exception)
            {
                Logger.Error($"Plugin manifest failed: {manifestPath}: {exception}");
            }
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Load(path)) ++loaded;
        }
        Logger.Info($"Managed plugin DLLs discovered: {Directory.EnumerateFiles(directory, "*.dll").Count()}");
        return loaded;
    }

    public bool Load(string path, string? expectedId = null)
    {
        PluginInstance? instance = null;
        try
        {
            var context = new PluginLoadContext(path);
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
            var pluginType = FindPluginType(assembly);
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
            instance = new PluginInstance(Path.GetFullPath(path), context, plugin, pluginId, new PluginResourceScope());
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
            plugin.OnLoad();
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
            if (!_plugins.TryGetValue(id, out old) || old.State != PluginState.Active) return false;
            _plugins.Remove(id);
        }

        var path = old.Path;
        old.Stop();
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
                callback(instance.Plugin);
                instance.ResetFailures();
            }
            catch
            {
                if (instance.RecordFailure(_failureThreshold) >= _failureThreshold)
                {
                    instance.Scope.Dispose();
                }
            }
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
        foreach (var instance in snapshot) instance.Stop();
    }

    public int Count
    {
        get { lock (_gate) return _plugins.Count; }
    }

    public bool IsActive(string id)
    {
        lock (_gate)
            return _plugins.TryGetValue(id, out var instance) && instance.State == PluginState.Active;
    }

    private static Type? FindPluginType(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(F4ForgePlugin).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>()
                .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(F4ForgePlugin).IsAssignableFrom(type))
                .FirstOrDefault();
        }
    }

    private sealed class PluginManifest
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
        [JsonPropertyName("runtime")] public string? Runtime { get; set; }
        [JsonPropertyName("minimumF4ForgeVersion")] public string? MinimumF4ForgeVersion { get; set; }
    }

    private sealed class PluginInstance
    {
        private readonly object _lifecycleGate = new();
        private int _inFlight;
        private PluginState _state = PluginState.Loading;
        private int _failures;

        public PluginInstance(string path, PluginLoadContext context, F4ForgePlugin plugin, string id, PluginResourceScope scope)
        {
            Path = path;
            Context = context;
            Plugin = plugin;
            Id = id;
            Scope = scope;
        }

        public string Path { get; }
        public PluginLoadContext Context { get; }
        public F4ForgePlugin Plugin { get; }
        public string Id { get; }
        public PluginResourceScope Scope { get; }
        public PluginState State
        {
            get { lock (_lifecycleGate) return _state; }
        }

        public bool Activate()
        {
            lock (_lifecycleGate)
            {
                if (_state != PluginState.Loading) return false;
                _state = PluginState.Active;
                return true;
            }
        }

        public bool TryAcquireDispatchLease(out IDisposable? lease)
        {
            lock (_lifecycleGate)
            {
                if (_state != PluginState.Active)
                {
                    lease = null;
                    return false;
                }
                ++_inFlight;
                lease = new DispatchLease(this);
                return true;
            }
        }

        public void ResetFailures()
        {
            lock (_lifecycleGate) _failures = 0;
        }

        public int RecordFailure(int failureThreshold)
        {
            lock (_lifecycleGate)
            {
                ++_failures;
                if (_failures >= failureThreshold) _state = PluginState.Disabled;
                return _failures;
            }
        }

        public void Stop()
        {
            bool wasLoaded;
            lock (_lifecycleGate)
            {
                if (_state is PluginState.Unloaded or PluginState.Quiescing) return;
                wasLoaded = _state is PluginState.Active or PluginState.Disabled;
                _state = PluginState.Quiescing;
                while (_inFlight != 0) Monitor.Wait(_lifecycleGate);
            }
            if (wasLoaded)
            {
                try { Plugin.OnUnload(); }
                catch { }
            }
            Scope.Dispose();
            lock (_lifecycleGate) _state = PluginState.Unloaded;
            Context.Unload();
        }

        private void ReleaseDispatchLease()
        {
            lock (_lifecycleGate)
            {
                --_inFlight;
                if (_inFlight == 0) Monitor.PulseAll(_lifecycleGate);
            }
        }

        private sealed class DispatchLease : IDisposable
        {
            private PluginInstance? _instance;

            public DispatchLease(PluginInstance instance) { _instance = instance; }

            public void Dispose()
            {
                Interlocked.Exchange(ref _instance, null)?.ReleaseDispatchLease();
            }
        }
    }
}
