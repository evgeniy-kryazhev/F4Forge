using System.Buffers.Binary;
using System.Text;
using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Tests;

public sealed class FormServiceTests
{
    [Fact]
    public void GetReturnsPopulatedNonOwningForm()
    {
        var bridge = new FormBridge();
        var service = CreateService(bridge);

        var result = service.Get(0x1234, out var form);

        Assert.Equal(F4ForgeResult.Success, result);
        Assert.NotNull(form);
        Assert.Equal(0x1234u, form.FormId);
        Assert.Equal(FormType.Item, form.FormType);
        Assert.Equal("Stimpak", form.Name);
        Assert.True(form.IsValid);
    }

    [Theory]
    [InlineData(F4ForgeResult.NotFound)]
    [InlineData(F4ForgeResult.NotReady)]
    public void GetCommunicatesEndpointFailures(F4ForgeResult failure)
    {
        var bridge = new FormBridge { InfoResult = failure };
        var result = CreateService(bridge).Get(1, out var form);
        Assert.Equal(failure, result);
        Assert.Null(form);
    }

    [Fact]
    public void GetRejectsInvalidNameContract()
    {
        var bridge = new FormBridge { InvalidNameLength = true };
        Assert.Equal(F4ForgeResult.InternalError, CreateService(bridge).Get(1, out _));
    }

    [Fact]
    public async Task GetAsyncMarshalsThroughGameThreadAndHonorsCancellation()
    {
        var scheduler = new RecordingScheduler();
        var service = CreateService(new FormBridge(), scheduler);
        var result = await service.GetAsync(0x1234);
        Assert.Equal(F4ForgeResult.Success, result.Result);
        Assert.Equal(1, scheduler.Calls);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => service.GetAsync(1, cancellation.Token));
    }

    [Fact]
    public void MissingEndpointsReturnCapabilityUnavailable()
    {
        var bridge = new FormBridge { ResolveEndpoints = false };
        Assert.Equal(F4ForgeResult.CapabilityUnavailable, CreateService(bridge).Get(1, out _));
    }

    private static FormService CreateService(FormBridge bridge, IGameThreadScheduler? scheduler = null)
    {
        var endpoints = new EndpointService(bridge, _ => { });
        return new FormService(endpoints, new GameThreadDispatcher(scheduler ?? new RecordingScheduler()));
    }

    private sealed class RecordingScheduler : IGameThreadScheduler
    {
        public int Calls { get; private set; }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            Calls++;
            action();
            return Task.CompletedTask;
        }

        public Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<TResult>(cancellationToken);
            Calls++;
            return Task.FromResult(action());
        }
    }

    private sealed class FormBridge : IPluginHostBridge
    {
        public F4ForgeResult InfoResult { get; init; } = F4ForgeResult.Success;
        public bool InvalidNameLength { get; init; }
        public bool ResolveEndpoints { get; init; } = true;

        public EndpointHandle ResolveEndpoint(string name, uint version) =>
            ResolveEndpoints ? new EndpointHandle(name.EndsWith("get_info", StringComparison.Ordinal) ? 1ul : 2ul) : default;

        public F4ForgeResult Invoke(EndpointHandle endpoint, ReadOnlySpan<byte> request, Span<byte> response, out uint responseSize)
        {
            if (endpoint.Value == 1)
            {
                responseSize = 12;
                if (InfoResult != F4ForgeResult.Success) return InfoResult;
                BinaryPrimitives.WriteUInt32LittleEndian(response, BinaryPrimitives.ReadUInt32LittleEndian(request));
                BinaryPrimitives.WriteUInt32LittleEndian(response[4..], (uint)FormType.Item);
                BinaryPrimitives.WriteUInt32LittleEndian(response[8..], 1);
                return F4ForgeResult.Success;
            }

            responseSize = 260;
            var bytes = Encoding.UTF8.GetBytes("Stimpak");
            BinaryPrimitives.WriteUInt32LittleEndian(response, InvalidNameLength ? 257u : (uint)bytes.Length);
            bytes.CopyTo(response[4..]);
            return F4ForgeResult.Success;
        }

        public HostRegistration<EndpointHandle>? RegisterEndpoint(string name, EndpointKind kind, uint version, uint requestSize, uint responseSize, ThreadPolicy threadPolicy, EndpointCallback callback) => null;
        public HostSubscription<EventSubscriptionHandle>? Subscribe(EndpointHandle endpoint, Action<ReadOnlyMemory<byte>> callback) => null;
        public HostSubscription<InterceptorSubscriptionHandle>? Intercept(EndpointHandle endpoint, Func<Memory<byte>, F4ForgeResult> callback) => null;
        public uint QueryCapability(string id, uint minimumVersion) => 0;
        public Task<AsyncOperationResult> InvokeAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsyncOperationResult> EmitAsync(EndpointHandle endpoint, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
