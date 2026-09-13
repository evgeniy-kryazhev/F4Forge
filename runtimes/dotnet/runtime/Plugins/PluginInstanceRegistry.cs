namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed class PluginInstanceRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PluginInstance> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<PluginInstance> _quarantined = [];

    public bool TryAdd(PluginInstance instance)
    {
        lock (_gate) return _active.TryAdd(instance.Id, instance);
    }

    public bool TryRemove(string id, out PluginInstance? instance)
    {
        lock (_gate) return _active.Remove(id, out instance);
    }

    public void Remove(PluginInstance instance)
    {
        lock (_gate)
            if (_active.TryGetValue(instance.Id, out var current) && ReferenceEquals(current, instance))
                _active.Remove(instance.Id);
    }

    public PluginInstance[] Drain()
    {
        lock (_gate)
        {
            var instances = _active.Values.ToArray();
            _active.Clear();
            return instances;
        }
    }

    public PluginInstance[] Snapshot()
    {
        lock (_gate) return _active.Values.ToArray();
    }

    public void Quarantine(PluginInstance instance)
    {
        lock (_gate) _quarantined.Add(instance);
        _ = instance.Stopped.ContinueWith(
            static (_, state) => ((QuarantineRemoval)state!).Remove(),
            new QuarantineRemoval(this, instance), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public bool IsActive(string id)
    {
        lock (_gate) return _active.TryGetValue(id, out var instance) && instance.State == PluginState.Active;
    }

    public bool IsQuarantined(string id)
    {
        lock (_gate) return _quarantined.Any(instance =>
            instance.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public int Count { get { lock (_gate) return _active.Count; } }
    private void RemoveQuarantine(PluginInstance instance)
    {
        lock (_gate) _quarantined.Remove(instance);
    }

    private sealed record QuarantineRemoval(PluginInstanceRegistry Registry, PluginInstance Instance)
    {
        public void Remove() => Registry.RemoveQuarantine(Instance);
    }
}
