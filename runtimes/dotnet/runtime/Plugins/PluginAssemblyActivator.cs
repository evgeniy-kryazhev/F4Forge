using System.Reflection;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Scheduling;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed unsafe class PluginAssemblyActivator(
    NativeApi* host, void* hostContext, ulong runtime, ManagedTaskScheduler? taskScheduler)
{
    public PluginInstance? Create(string path, string? expectedId = null)
    {
        var fullPath = Path.GetFullPath(path);
        var loadContext = new PluginLoadContext(fullPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);
            var plugin = CreatePlugin(assembly, expectedId);
            if (plugin == null)
            {
                loadContext.Unload();
                return null;
            }
            if (string.IsNullOrWhiteSpace(plugin.Id))
                throw new InvalidOperationException("Plugin ID must not be empty.");
            if (expectedId != null && !plugin.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Manifest ID '{expectedId}' does not match plugin ID '{plugin.Id}'.");
            return new PluginInstance(fullPath, loadContext, plugin, plugin.Id, new PluginResourceScope(),
                host, hostContext, runtime, taskScheduler);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    private static F4ForgePlugin? CreatePlugin(Assembly assembly, string? expectedId)
    {
        try { return CreatePlugin(assembly.GetTypes(), expectedId); }
        catch (ReflectionTypeLoadException exception) { return CreatePlugin(exception.Types.OfType<Type>(), expectedId); }
    }

    private static F4ForgePlugin? CreatePlugin(IEnumerable<Type> types, string? expectedId)
    {
        var candidates = types.Where(type => type is { IsAbstract: false, IsPublic: true } &&
                typeof(F4ForgePlugin).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
        foreach (var type in candidates)
        {
            if (Activator.CreateInstance(type) is not F4ForgePlugin plugin) continue;
            if (expectedId == null || plugin.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase)) return plugin;
        }
        return null;
    }
}
