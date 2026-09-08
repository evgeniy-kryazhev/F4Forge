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
    private const string FrameworkVersion = "0.1.0";
    private readonly object _gate = new();
    private readonly Dictionary<string, PluginInstance> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _failureThreshold;
    private static readonly AsyncLocal<int> DispatchDepth = new();

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
        var candidates = new List<PluginCandidate>();

        foreach (var pluginDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(pluginDirectory, "f4forge.plugin.json");
            if (!File.Exists(manifestPath)) continue;
            try
            {
                var manifest = ReadManifest(manifestPath);
                if (manifest == null || !ValidateManifest(manifest, manifestPath)) continue;
                var root = Path.GetFullPath(pluginDirectory);
                var assemblyPath = Path.GetFullPath(Path.Combine(root, manifest.EntryAssembly!));
                if (!IsWithinDirectory(root, assemblyPath) || !File.Exists(assemblyPath))
                {
                    Logger.Error($"Plugin entry assembly is outside its directory or missing: {manifestPath}");
                    continue;
                }
                candidates.Add(new PluginCandidate(assemblyPath, manifest));
            }
            catch (Exception exception)
            {
                Logger.Error($"Plugin manifest failed: {manifestPath}: {exception}");
            }
        }

        var loaded = 0;
        foreach (var candidate in OrderCandidates(candidates))
        {
            if (!DependenciesActive(candidate, candidates))
            {
                Logger.Error($"Managed plugin '{candidate.Manifest.Id}' skipped because a dependency is inactive.");
                continue;
            }
            if (Load(candidate.Path, candidate.Manifest.Id)) ++loaded;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Load(path)) ++loaded;
        }
        Logger.Info($"Managed plugin DLLs discovered: {Directory.EnumerateFiles(directory, "*.dll").Count()}");
        return loaded;
    }

    private static PluginManifest? ReadManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path));
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            Logger.Error($"Invalid plugin manifest: {path}");
            return null;
        }
        if (!string.IsNullOrWhiteSpace(manifest.Runtime) &&
            !manifest.Runtime.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return null;
        return manifest;
    }

    private static bool ValidateManifest(PluginManifest manifest, string path)
    {
        var id = string.IsNullOrWhiteSpace(manifest.Id) ? path : manifest.Id;
        if (!TryParseVersion(manifest.Version, out _))
        {
            Logger.Error($"Plugin '{id}' has malformed version in {path}.");
            return false;
        }
        if (!TryParseVersion(manifest.MinimumF4ForgeVersion, out var minimum))
        {
            Logger.Error($"Plugin '{id}' has malformed minimumF4ForgeVersion in {path}.");
            return false;
        }
        if (minimum > new Version(FrameworkVersion))
        {
            Logger.Error($"Plugin '{id}' requires F4Forge {minimum}, current framework version is {FrameworkVersion}.");
            return false;
        }
        return true;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            version = new Version(0, 0);
            return true;
        }
        return Version.TryParse(value, out version!);
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var root = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static List<PluginCandidate> OrderCandidates(IReadOnlyList<PluginCandidate> candidates)
    {
        var ids = new Dictionary<string, PluginCandidate>(StringComparer.OrdinalIgnoreCase);
        var capabilities = new Dictionary<string, PluginCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Manifest.Id))
            {
                if (!ids.TryAdd(candidate.Manifest.Id!, candidate))
                    Logger.Error($"Duplicate managed plugin id: {candidate.Manifest.Id}");
            }
            foreach (var capability in candidate.Manifest.ProvidedCapabilities ?? [])
                if (!capabilities.TryAdd(capability, candidate))
                    Logger.Error($"Duplicate managed capability provider: {capability}");
        }

        if (ids.Count != candidates.Count(candidate => !string.IsNullOrWhiteSpace(candidate.Manifest.Id)) ||
            capabilities.Count != (candidates.SelectMany(candidate => candidate.Manifest.ProvidedCapabilities ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            return [];

        var providers = new Dictionary<string, PluginCandidate>(ids, StringComparer.OrdinalIgnoreCase);
        foreach (var capability in capabilities)
            if (!providers.TryAdd(capability.Key, capability.Value))
            {
                Logger.Error($"Managed plugin id/capability collision: {capability.Key}");
                return [];
            }

        var visiting = new HashSet<PluginCandidate>();
        var visited = new HashSet<PluginCandidate>();
        var failed = new HashSet<PluginCandidate>();
        var ordered = new List<PluginCandidate>();
        bool Visit(PluginCandidate candidate)
        {
            if (visited.Contains(candidate)) return true;
            if (failed.Contains(candidate)) return false;
            if (!visiting.Add(candidate))
            {
                Logger.Error($"Managed plugin dependency cycle includes '{candidate.Manifest.Id}'.");
                failed.Add(candidate);
                return false;
            }
            var valid = true;
            foreach (var dependency in candidate.Manifest.Dependencies ?? [])
            {
                if (!providers.TryGetValue(dependency, out var provider))
                {
                    Logger.Error($"Managed plugin '{candidate.Manifest.Id}' is missing dependency '{dependency}'.");
                    valid = false;
                    break;
                }
                if (!Visit(provider))
                {
                    valid = false;
                    break;
                }
            }
            visiting.Remove(candidate);
            if (!valid)
            {
                failed.Add(candidate);
                return false;
            }
            visited.Add(candidate);
            ordered.Add(candidate);
            return true;
        }

        foreach (var candidate in candidates)
            Visit(candidate);
        return ordered;
    }

    private bool DependenciesActive(
        PluginCandidate candidate,
        IReadOnlyList<PluginCandidate> candidates)
    {
        foreach (var dependency in candidate.Manifest.Dependencies ?? [])
        {
            var provider = candidates.FirstOrDefault(item =>
                string.Equals(item.Manifest.Id, dependency, StringComparison.OrdinalIgnoreCase) ||
                (item.Manifest.ProvidedCapabilities ?? [])
                    .Any(capability => string.Equals(capability, dependency, StringComparison.OrdinalIgnoreCase)));
            if (provider == null || string.IsNullOrWhiteSpace(provider.Manifest.Id) ||
                !IsActive(provider.Manifest.Id!)) return false;
        }
        return true;
    }

    public bool Load(string path, string? expectedId = null)
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
                ++DispatchDepth.Value;
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
            finally
            {
                --DispatchDepth.Value;
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

    private static Type? FindPluginType(Assembly assembly, string? expectedId = null)
    {
        try
        {
            var types = assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(F4ForgePlugin).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
            if (expectedId == null) return types.FirstOrDefault();
            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is F4ForgePlugin plugin &&
                    plugin.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase)) return type;
            }
            return null;
        }
        catch (ReflectionTypeLoadException exception)
        {
            var types = exception.Types.OfType<Type>()
                .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(F4ForgePlugin).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);
            if (expectedId == null) return types.FirstOrDefault();
            foreach (var type in types)
            {
                if (Activator.CreateInstance(type) is F4ForgePlugin plugin &&
                    plugin.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase)) return type;
            }
            return null;
        }
    }

    private sealed class PluginManifest
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("entryAssembly")] public string? EntryAssembly { get; set; }
        [JsonPropertyName("runtime")] public string? Runtime { get; set; }
        [JsonPropertyName("minimumF4ForgeVersion")] public string? MinimumF4ForgeVersion { get; set; }
        [JsonPropertyName("dependencies")] public string[]? Dependencies { get; set; }
        [JsonPropertyName("providedCapabilities")] public string[]? ProvidedCapabilities { get; set; }
    }

    private sealed record PluginCandidate(string Path, PluginManifest Manifest);

    private sealed class PluginInstance
    {
        private readonly object _lifecycleGate = new();
        private readonly TaskCompletionSource<bool> _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _inFlight;
        private PluginState _state = PluginState.Loading;
        private int _failures;
        private bool _teardownStarted;

        public PluginInstance(string path, PluginLoadContext context, F4ForgePlugin plugin, string id, PluginResourceScope scope)
        {
            Path = path;
            Context = context;
            Plugin = plugin;
            Id = id;
            Scope = scope;
            ContextInfo = new F4ForgePluginContext(
                F4Forge.DotNet.Sdk.ModuleHandle.Invalid,
                scope.Add,
                scope.CancellationToken);
        }

        public string Path { get; }
        public PluginLoadContext Context { get; }
        public F4ForgePlugin Plugin { get; }
        public string Id { get; }
        public PluginResourceScope Scope { get; }
        public F4ForgePluginContext ContextInfo { get; }
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
            bool finalize;
            lock (_lifecycleGate)
            {
                if (_state == PluginState.Unloaded) return;
                if (_state == PluginState.Quiescing)
                {
                    if (DispatchDepth.Value == 0) _stopped.Task.GetAwaiter().GetResult();
                    return;
                }
                wasLoaded = _state is PluginState.Active or PluginState.Disabled;
                _state = PluginState.Quiescing;
                finalize = _inFlight == 0;
            }
            if (finalize) FinalizeStop(wasLoaded);
            else if (DispatchDepth.Value == 0) _stopped.Task.GetAwaiter().GetResult();
        }

        private void FinalizeStop(bool wasLoaded)
        {
            lock (_lifecycleGate)
            {
                if (_teardownStarted || _state == PluginState.Unloaded) return;
                _teardownStarted = true;
            }
            if (wasLoaded)
            {
                try { Plugin.OnUnload(); }
                catch { }
            }
            Scope.Dispose();
            lock (_lifecycleGate) _state = PluginState.Unloaded;
            Context.Unload();
            _stopped.TrySetResult(true);
        }

        private void ReleaseDispatchLease()
        {
            bool finalize;
            lock (_lifecycleGate)
            {
                --_inFlight;
                finalize = _inFlight == 0 && _state == PluginState.Quiescing;
            }
            if (finalize) FinalizeStop(true);
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
