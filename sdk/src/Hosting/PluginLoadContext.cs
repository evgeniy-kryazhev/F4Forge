using System.Reflection;
using System.Runtime.Loader;

namespace F4Forge.Hosting;

using F4Forge.Plugins;

internal sealed class PluginLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
    private readonly string _directory = Path.GetDirectoryName(assemblyPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var sdkAssembly = typeof(IPlugin).Assembly;
        if (assemblyName.Name == sdkAssembly.GetName().Name)
        {
            return sdkAssembly;
        }
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var adjacentAssembly = Path.Combine(_directory, assemblyName.Name + ".dll");
            if (File.Exists(adjacentAssembly))
            {
                path = adjacentAssembly;
            }
        }
        if (path is null)
        {
            return null;
        }
        // Dependency DLLs must remain replaceable, just like the plugin's main DLL.
        using var assemblyStream = File.OpenRead(path);
        return LoadFromStream(assemblyStream);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
