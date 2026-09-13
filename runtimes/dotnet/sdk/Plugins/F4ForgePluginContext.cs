namespace F4Forge.DotNet.Sdk;

public sealed class F4ForgePluginContext
{
    private readonly Action<IDisposable> _trackResource;
    internal F4ForgePluginContext(
        ModuleHandle module,
        Action<IDisposable> trackResource,
        IPluginHostBridge? bridge,
        CancellationToken cancellationToken,
        PluginEvents? events = null,
        InputEvents? input = null,
        IGameThreadScheduler? gameThreadScheduler = null)
    {
        Module = module;
        CancellationToken = cancellationToken;
        _trackResource = trackResource;
        Events = events ?? new PluginEvents();
        Input = input ?? new InputEvents();
        Endpoints = new EndpointService(bridge, Track);
        Capabilities = new CapabilityService(bridge);
        GameThread = new GameThreadDispatcher(gameThreadScheduler);
    }

    public ModuleHandle Module { get; }
    public CancellationToken CancellationToken { get; }
    public PluginEvents Events { get; }
    public InputEvents Input { get; }
    public EndpointService Endpoints { get; }
    public CapabilityService Capabilities { get; }
    public GameThreadDispatcher GameThread { get; }

    public EndpointHandle ResolveEndpoint(string name, uint version = 1)
    {
        return Endpoints.Resolve(name, version);
    }

    public EndpointHandle RegisterEndpoint(
        string name,
        EndpointKind kind,
        uint version,
        uint requestSize,
        uint responseSize,
        EndpointCallback callback,
        ThreadPolicy threadPolicy = ThreadPolicy.Any)
    {
        return Endpoints.Register(name, kind, version, requestSize, responseSize, callback, threadPolicy);
    }

    public F4ForgeResult Invoke(
        EndpointHandle endpoint,
        ReadOnlySpan<byte> request,
        Span<byte> response,
        out uint responseSize)
    {
        return Endpoints.Invoke(endpoint, request, response, out responseSize);
    }

    public EventSubscriptionHandle Subscribe(
        EndpointHandle endpoint,
        Action<ReadOnlyMemory<byte>> callback)
    {
        return Endpoints.Subscribe(endpoint, callback);
    }

    public InterceptorSubscriptionHandle Intercept(
        EndpointHandle endpoint,
        Func<Memory<byte>, F4ForgeResult> callback)
    {
        return Endpoints.Intercept(endpoint, callback);
    }

    public uint QueryCapability(string id, uint minimumVersion = 1)
    {
        return Capabilities.Query(id, minimumVersion);
    }

    public Task<AsyncOperationResult> InvokeAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken = default)
        => Endpoints.InvokeAsync(endpoint, request, cancellationToken);

    public Task<AsyncOperationResult> EmitAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
        => Endpoints.EmitAsync(endpoint, payload, cancellationToken);

    public void Track(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _trackResource(resource);
    }
}
