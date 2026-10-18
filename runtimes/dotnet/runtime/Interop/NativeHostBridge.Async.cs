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
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AsyncOperationResult>(cancellationToken);
        }
        if (_api == null)
        {
            return Task.FromResult(new AsyncOperationResult(
                F4ForgeResult.InactiveRuntime, [], F4ForgeResult.InactiveRuntime));
        }
        var copy = data.ToArray();
        var created = CreateOperation(endpoint, copy, emit);
        if (created.Result != F4ForgeResult.Success)
        {
            return Task.FromResult(new AsyncOperationResult(created.Result, [], created.Result));
        }
        // Once created, the native handle needs collection even if cancellation wins before scheduling.
        return Task.Run(() => WaitAndCollect(created.Operation, cancellationToken), CancellationToken.None);
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
        try
        {
            using var registration = cancellationToken.Register(() => _api->CancelOperation(_context, operation));
            var waitResult = (F4ForgeResult)_api->WaitOperation(_context, operation, uint.MaxValue);
            if (waitResult != F4ForgeResult.Success)
            {
                return new AsyncOperationResult(waitResult, [], waitResult);
            }

            var response = new byte[64 * 1024];
            var collectionResult = CollectResponse(operation, response, out var responseSize, out var invocationResult);
            if (collectionResult == F4ForgeResult.BufferTooSmall &&
                responseSize > response.Length && responseSize <= Array.MaxLength)
            {
                // A terminal operation's response is immutable, so one exact-size retry is sufficient.
                response = new byte[checked((int)responseSize)];
                collectionResult = CollectResponse(operation, response, out responseSize, out invocationResult);
            }
            if (collectionResult != F4ForgeResult.Success)
            {
                return new AsyncOperationResult(waitResult, [], collectionResult);
            }
            if (responseSize > response.Length)
            {
                return new AsyncOperationResult(waitResult, [], F4ForgeResult.InternalError);
            }

            Array.Resize(ref response, checked((int)responseSize));
            return new AsyncOperationResult(waitResult, response, invocationResult);
        }
        finally
        {
            _api->ReleaseOperation(_context, operation);
        }
    }

    private F4ForgeResult CollectResponse(ulong operation, byte[] response,
        out uint responseSize, out F4ForgeResult invocation)
    {
        uint size = 0;
        var invocationResult = (int)F4ForgeResult.InternalError;
        fixed (byte* responsePointer = response)
        {
            var result = (F4ForgeResult)_api->GetOperationResult(_context, operation, &invocationResult,
                responsePointer, (uint)response.Length, &size);
            responseSize = size;
            invocation = (F4ForgeResult)invocationResult;
            return result;
        }
    }
}
