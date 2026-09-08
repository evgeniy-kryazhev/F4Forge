using System.Reflection;
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
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll").Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Load(path)) ++loaded;
        }
        Logger.Info($"Managed plugin DLLs discovered: {Directory.EnumerateFiles(directory, "*.dll").Count()}");
        return loaded;
    }

    public bool Load(string path)
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
            lock (_gate) instance.State = PluginState.Active;
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
            if (!_plugins.Remove(id, out old)) return false;
            old.State = PluginState.Quiescing;
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
            if (instance.State != PluginState.Active) continue;
            try
            {
                callback(instance.Plugin);
                instance.Failures = 0;
            }
            catch
            {
                ++instance.Failures;
                if (instance.Failures >= _failureThreshold)
                {
                    instance.State = PluginState.Disabled;
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

    private sealed class PluginInstance
    {
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
        public PluginState State { get; set; } = PluginState.Loading;
        public int Failures { get; set; }

        public void Stop()
        {
            if (State == PluginState.Unloaded) return;
            var wasLoaded = State is PluginState.Active or PluginState.Disabled;
            State = PluginState.Quiescing;
            if (wasLoaded)
            {
                try { Plugin.OnUnload(); }
                catch { }
            }
            Scope.Dispose();
            State = PluginState.Unloaded;
            Context.Unload();
        }
    }
}
