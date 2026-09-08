namespace F4Forge.DotNet.Runtime;

internal sealed class PluginResourceScope : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly List<IDisposable> _resources = [];
    private int _disposed;

    public CancellationToken CancellationToken => _cancellation.Token;

    public void Add(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_resources)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _resources.Add(resource);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cancellation.Cancel();
        IDisposable[] resources;
        lock (_resources)
            resources = _resources.ToArray();
        foreach (var resource in resources.Reverse())
        {
            try { resource.Dispose(); }
            catch { }
        }
        _cancellation.Dispose();
    }
}
