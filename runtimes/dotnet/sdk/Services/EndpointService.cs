namespace F4Forge.DotNet.Sdk;

public sealed class EndpointService
{
    private readonly IPluginHostBridge? _bridge;
    private readonly Action<IDisposable> _trackResource;

    internal EndpointService(IPluginHostBridge? bridge, Action<IDisposable> trackResource)
    {
        _bridge = bridge;
        _trackResource = trackResource;
    }

    public EndpointHandle Resolve(string name, uint version = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return _bridge?.ResolveEndpoint(name, version) ?? default;
    }

    public EndpointHandle Register(
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
        if (registration is null)
        {
            return default;
        }

        _trackResource(registration.Resource);
        return registration.Handle;
    }

    public F4ForgeResult Invoke(
        EndpointHandle endpoint,
        ReadOnlySpan<byte> request,
        Span<byte> response,
        out uint responseSize)
    {
        if (_bridge is not null)
        {
            return _bridge.Invoke(endpoint, request, response, out responseSize);
        }

        responseSize = 0;
        return F4ForgeResult.InactiveRuntime;
    }

    public EventSubscriptionHandle Subscribe(EndpointHandle endpoint, Action<ReadOnlyMemory<byte>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var subscription = _bridge?.Subscribe(endpoint, callback);
        if (subscription is null)
        {
            return default;
        }

        _trackResource(subscription.Resource);
        return subscription.Handle;
    }

    public InterceptorSubscriptionHandle Intercept(
        EndpointHandle endpoint,
        Func<Memory<byte>, F4ForgeResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var subscription = _bridge?.Intercept(endpoint, callback);
        if (subscription is null)
        {
            return default;
        }

        _trackResource(subscription.Resource);
        return subscription.Handle;
    }

    public Task<AsyncOperationResult> InvokeAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken = default) =>
        _bridge?.InvokeAsync(endpoint, request, cancellationToken) ?? InactiveResult();

    public Task<AsyncOperationResult> EmitAsync(
        EndpointHandle endpoint,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default) =>
        _bridge?.EmitAsync(endpoint, payload, cancellationToken) ?? InactiveResult();

    private static Task<AsyncOperationResult> InactiveResult() =>
        Task.FromResult(new AsyncOperationResult(F4ForgeResult.InactiveRuntime, [], F4ForgeResult.InactiveRuntime));
}
