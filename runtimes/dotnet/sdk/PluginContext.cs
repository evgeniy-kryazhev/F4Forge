namespace F4Forge.DotNet.Sdk;

public sealed class F4ForgePluginContext
{
    private readonly Action<IDisposable> _trackResource;
    private readonly IPluginHostBridge? _bridge;

    internal F4ForgePluginContext(
        ModuleHandle module,
        Action<IDisposable> trackResource,
        IPluginHostBridge? bridge,
        CancellationToken cancellationToken)
    {
        Module = module;
        CancellationToken = cancellationToken;
        _trackResource = trackResource;
        _bridge = bridge;
    }

    public ModuleHandle Module { get; }
    public CancellationToken CancellationToken { get; }

    public EndpointHandle ResolveEndpoint(string name, uint version = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _bridge?.ResolveEndpoint(name, version) ?? default;
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
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(callback);
        var registration = _bridge?.RegisterEndpoint(
            name, kind, version, requestSize, responseSize, threadPolicy, callback);
        if (registration is null) return default;
        Track(registration.Resource);
        return registration.Handle;
    }

    public F4ForgeResult Invoke(
        EndpointHandle endpoint,
        ReadOnlySpan<byte> request,
        Span<byte> response,
        out uint responseSize)
    {
        if (_bridge == null) { responseSize = 0; return F4ForgeResult.InactiveRuntime; }
        return _bridge.Invoke(endpoint, request, response, out responseSize);
    }

    public EventSubscriptionHandle Subscribe(
        EndpointHandle endpoint,
        Action<ReadOnlyMemory<byte>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var subscription = _bridge?.Subscribe(endpoint, callback);
        if (subscription is null) return default;
        Track(subscription.Resource);
        return subscription.Handle;
    }

    public InterceptorSubscriptionHandle Intercept(
        EndpointHandle endpoint,
        Func<Memory<byte>, F4ForgeResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var subscription = _bridge?.Intercept(endpoint, callback);
        if (subscription is null) return default;
        Track(subscription.Resource);
        return subscription.Handle;
    }

    public uint QueryCapability(string id, uint minimumVersion = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _bridge?.QueryCapability(id, minimumVersion) ?? 0;
    }

    public Task<AsyncOperationResult> InvokeAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken = default)
        => _bridge?.InvokeAsync(endpoint, request, cancellationToken) ??
            Task.FromResult(new AsyncOperationResult(F4ForgeResult.InactiveRuntime, [], F4ForgeResult.InactiveRuntime));

    public Task<AsyncOperationResult> EmitAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
        => _bridge?.EmitAsync(endpoint, payload, cancellationToken) ??
            Task.FromResult(new AsyncOperationResult(F4ForgeResult.InactiveRuntime, [], F4ForgeResult.InactiveRuntime));

    public void Track(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _trackResource(resource);
    }
}

internal interface IPluginHostBridge
{
    EndpointHandle ResolveEndpoint(string name, uint version);
    HostRegistration<EndpointHandle>? RegisterEndpoint(
        string name, EndpointKind kind, uint version, uint requestSize,
        uint responseSize, ThreadPolicy threadPolicy, EndpointCallback callback);
    F4ForgeResult Invoke(EndpointHandle endpoint, ReadOnlySpan<byte> request, Span<byte> response, out uint responseSize);
    HostSubscription<EventSubscriptionHandle>? Subscribe(EndpointHandle endpoint, Action<ReadOnlyMemory<byte>> callback);
    HostSubscription<InterceptorSubscriptionHandle>? Intercept(EndpointHandle endpoint, Func<Memory<byte>, F4ForgeResult> callback);
    uint QueryCapability(string id, uint minimumVersion);
    Task<AsyncOperationResult> InvokeAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken);
    Task<AsyncOperationResult> EmitAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}

internal sealed class HostSubscription<THandle> where THandle : struct
{
    public required THandle Handle { get; init; }
    public required IDisposable Resource { get; init; }
}
