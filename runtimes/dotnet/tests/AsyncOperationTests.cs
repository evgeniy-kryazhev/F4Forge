using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed class AsyncOperationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledCallDoesNotCreateNativeOperation(bool emit)
    {
        using var harness = new OperationHarness();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Start(emit, cancellation.Token));

        Assert.Equal(0, harness.Created);
        Assert.Equal(0, harness.Released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringCreationStillCollectsAndReleasesOperation(bool emit)
    {
        using var harness = new OperationHarness { CollectionResult = F4ForgeResult.OperationCancelled };
        using var cancellation = new CancellationTokenSource();
        harness.OnCreate = cancellation.Cancel;

        var result = await harness.Start(emit, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(F4ForgeResult.Success, result.WaitResult);
        Assert.Equal(F4ForgeResult.OperationCancelled, result.InvocationResult);
        Assert.Empty(result.Response);
        Assert.Equal(1, harness.Created);
        Assert.Equal(1, harness.Cancelled);
        Assert.Equal(1, harness.Released);
    }

    [Theory]
    [InlineData(F4ForgeResult.OperationCancelled)]
    [InlineData(F4ForgeResult.InvalidHandle)]
    [InlineData(F4ForgeResult.InternalError)]
    public async Task CollectionFailureIsReturnedWithoutResponseData(F4ForgeResult failure)
    {
        using var harness = new OperationHarness { CollectionResult = failure };

        var result = await harness.Start(false, CancellationToken.None);

        Assert.Equal(F4ForgeResult.Success, result.WaitResult);
        Assert.Equal(failure, result.InvocationResult);
        Assert.Empty(result.Response);
        Assert.Equal(1, harness.Released);
    }

    [Fact]
    public async Task WaitFailureIsReturnedWithoutAttemptingCollection()
    {
        using var harness = new OperationHarness { WaitResult = F4ForgeResult.InvalidHandle };

        var result = await harness.Start(false, CancellationToken.None);

        Assert.Equal(F4ForgeResult.InvalidHandle, result.WaitResult);
        Assert.Equal(F4ForgeResult.InvalidHandle, result.InvocationResult);
        Assert.Empty(result.Response);
        Assert.Equal(0, harness.Collections);
        Assert.Equal(1, harness.Released);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(65536)]
    [InlineData(70000)]
    public async Task ResponseIsCollectedInFullAndOperationReleased(int responseSize)
    {
        var expected = Enumerable.Range(0, responseSize).Select(index => (byte)(index % 251)).ToArray();
        using var harness = new OperationHarness { Response = expected };

        var result = await harness.Start(false, CancellationToken.None);

        Assert.Equal(F4ForgeResult.Success, result.WaitResult);
        Assert.Equal(F4ForgeResult.Success, result.InvocationResult);
        Assert.Equal(expected, result.Response);
        Assert.Equal(responseSize > 65536 ? 2 : 1, harness.Collections);
        Assert.Equal(1, harness.Released);
    }

    [Fact]
    public async Task EndpointFailureIsPreservedAfterSuccessfulCollection()
    {
        using var harness = new OperationHarness { InvocationResult = F4ForgeResult.NotFound };

        var result = await harness.Start(false, CancellationToken.None);

        Assert.Equal(F4ForgeResult.Success, result.WaitResult);
        Assert.Equal(F4ForgeResult.NotFound, result.InvocationResult);
        Assert.Equal(1, harness.Released);
    }

    private sealed unsafe class OperationHarness : IDisposable
    {
        private readonly NativeApi* _api;
        private readonly GCHandle _context;
        private readonly NativeHostBridge _bridge;

        public OperationHarness()
        {
            _context = GCHandle.Alloc(this);
            _api = (NativeApi*)NativeMemory.AllocZeroed((nuint)sizeof(NativeApi));
            _api->InvokeAsync = &CreateOperation;
            _api->EmitAsync = &CreateOperation;
            _api->WaitOperation = &WaitOperation;
            _api->GetOperationResult = &GetResult;
            _api->CancelOperation = &CancelOperation;
            _api->ReleaseOperation = &ReleaseOperation;
            _bridge = new NativeHostBridge(_api, (void*)GCHandle.ToIntPtr(_context), 0, "async-test");
        }

        public int Created { get; private set; }
        public int Released { get; private set; }
        public int Cancelled { get; private set; }
        public int Collections { get; private set; }
        public Action? OnCreate { get; set; }
        public byte[] Response { get; init; } = [];
        public F4ForgeResult WaitResult { get; init; } = F4ForgeResult.Success;
        public F4ForgeResult CollectionResult { get; init; } = F4ForgeResult.Success;
        public F4ForgeResult InvocationResult { get; init; } = F4ForgeResult.Success;

        public Task<AsyncOperationResult> Start(bool emit, CancellationToken cancellationToken)
        {
            return emit
                ? _bridge.EmitAsync(new EndpointHandle(1), ReadOnlyMemory<byte>.Empty, cancellationToken)
                : _bridge.InvokeAsync(new EndpointHandle(1), ReadOnlyMemory<byte>.Empty, cancellationToken);
        }

        public void Dispose()
        {
            _bridge.Dispose();
            NativeMemory.Free(_api);
            _context.Free();
        }

        private static OperationHarness GetHarness(void* context)
        {
            return (OperationHarness)GCHandle.FromIntPtr((nint)context).Target!;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int CreateOperation(void* context, ulong module, ulong endpoint, void* data, uint size, ulong* operation)
        {
            var harness = GetHarness(context);
            ++harness.Created;
            *operation = 1;
            harness.OnCreate?.Invoke();
            return (int)F4ForgeResult.Success;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int WaitOperation(void* context, ulong operation, uint timeout)
        {
            return (int)GetHarness(context).WaitResult;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int GetResult(void* context, ulong operation, int* invocation,
            void* response, uint capacity, uint* responseSize)
        {
            var harness = GetHarness(context);
            ++harness.Collections;
            if (harness.CollectionResult != F4ForgeResult.Success)
            {
                return (int)harness.CollectionResult;
            }

            *responseSize = (uint)harness.Response.Length;
            if (capacity < *responseSize)
            {
                return (int)F4ForgeResult.BufferTooSmall;
            }

            harness.Response.CopyTo(new Span<byte>(response, checked((int)capacity)));
            *invocation = (int)harness.InvocationResult;
            return (int)F4ForgeResult.Success;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int CancelOperation(void* context, ulong operation)
        {
            ++GetHarness(context).Cancelled;
            return (int)F4ForgeResult.Success;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static int ReleaseOperation(void* context, ulong operation)
        {
            ++GetHarness(context).Released;
            return (int)F4ForgeResult.Success;
        }
    }
}
