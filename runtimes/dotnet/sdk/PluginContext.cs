namespace F4Forge.DotNet.Sdk;

public sealed class F4ForgePluginContext
{
    private readonly Action<IDisposable> _trackResource;

    internal F4ForgePluginContext(
        ModuleHandle module,
        Action<IDisposable> trackResource,
        CancellationToken cancellationToken)
    {
        Module = module;
        CancellationToken = cancellationToken;
        _trackResource = trackResource;
    }

    public ModuleHandle Module { get; }
    public CancellationToken CancellationToken { get; }

    public void Track(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _trackResource(resource);
    }
}
