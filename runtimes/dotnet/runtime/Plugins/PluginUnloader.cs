namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed class PluginUnloader(PluginInstanceRegistry registry)
{
    public bool Stop(PluginInstance instance)
    {
        if (instance.Stop()) return true;
        registry.Quarantine(instance);
        return false;
    }

    public void StopAll()
    {
        foreach (var instance in registry.Drain()) Stop(instance);
    }
}
