using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Interop;

internal unsafe sealed partial class NativeHostBridge
{
    public Task<AsyncOperationResult> InvokeAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> request,
        CancellationToken cancellationToken) => StartAsync(endpoint, request, false, cancellationToken);

    public Task<AsyncOperationResult> EmitAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken) => StartAsync(endpoint, payload, true, cancellationToken);

    private Task<AsyncOperationResult> StartAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> data,
        bool emit, CancellationToken cancellationToken)
    {
        if (_api == null) return Task.FromResult(new AsyncOperationResult(
            F4ForgeResult.InactiveRuntime, [], F4ForgeResult.InactiveRuntime));
        var copy = data.ToArray();
        var created = CreateOperation(endpoint, copy, emit);
        if (created.Result != F4ForgeResult.Success)
            return Task.FromResult(new AsyncOperationResult(created.Result, [], created.Result));
        return Task.Run(() => WaitAndCollect(created.Operation, cancellationToken), cancellationToken);
    }

    private (F4ForgeResult Result, ulong Operation) CreateOperation(EndpointHandle endpoint, byte[] data, bool emit)
    {
        ulong operation = 0;
        fixed (byte* bytes = data)
        {
            var result = emit
                ? _api->EmitAsync(_context, _module.Value, endpoint.Value, bytes, (uint)data.Length, &operation)
                : _api->InvokeAsync(_context, _module.Value, endpoint.Value, bytes, (uint)data.Length, &operation);
            return ((F4ForgeResult)result, operation);
        }
    }

    private AsyncOperationResult WaitAndCollect(ulong operation, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _api->CancelOperation(_context, operation));
        var wait = (F4ForgeResult)_api->WaitOperation(_context, operation, uint.MaxValue);
        var response = new byte[64 * 1024];
        uint responseSize = 0;
        var invocation = 0;
        F4ForgeResult get;
        fixed (byte* responsePointer = response)
        {
            get = (F4ForgeResult)_api->GetOperationResult(_context, operation, &invocation,
                responsePointer, (uint)response.Length, &responseSize);
        }
        if (get == F4ForgeResult.BufferTooSmall) response = [];
        else if (get == F4ForgeResult.Success) Array.Resize(ref response, (int)responseSize);
        _api->ReleaseOperation(_context, operation);
        return new(wait, response, (F4ForgeResult)invocation);
    }
}
