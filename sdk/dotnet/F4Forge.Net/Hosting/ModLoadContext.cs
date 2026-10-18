using System.Reflection;
using System.Runtime.Loader;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Hosting;

internal sealed class ModLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
    private readonly string _directory = Path.GetDirectoryName(assemblyPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var sdkAssembly = typeof(IMod).Assembly;
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
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
