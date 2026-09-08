using System.Reflection;
using System.Runtime.Loader;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly Assembly _sharedSdk = typeof(F4ForgePlugin).Assembly;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name == _sharedSdk.GetName().Name)
            return _sharedSdk;
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path == null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
