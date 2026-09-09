namespace F4Forge.DotNet.Sdk;

internal interface IPluginHostBridge
{
    EndpointHandle ResolveEndpoint(string name, uint version);
    HostRegistration<EndpointHandle>? RegisterEndpoint(string name, EndpointKind kind, uint version,
        uint requestSize, uint responseSize, ThreadPolicy threadPolicy, EndpointCallback callback);
    F4ForgeResult Invoke(EndpointHandle endpoint, ReadOnlySpan<byte> request, Span<byte> response, out uint responseSize);
    HostSubscription<EventSubscriptionHandle>? Subscribe(EndpointHandle endpoint, Action<ReadOnlyMemory<byte>> callback);
    HostSubscription<InterceptorSubscriptionHandle>? Intercept(EndpointHandle endpoint, Func<Memory<byte>, F4ForgeResult> callback);
    uint QueryCapability(string id, uint minimumVersion);
    Task<AsyncOperationResult> InvokeAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken);
    Task<AsyncOperationResult> EmitAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}

internal sealed class HostRegistration<THandle> where THandle : struct
{
    public required THandle Handle { get; init; }
    public required IDisposable Resource { get; init; }
}

internal sealed class HostSubscription<THandle> where THandle : struct
{
    public required THandle Handle { get; init; }
    public required IDisposable Resource { get; init; }
}
