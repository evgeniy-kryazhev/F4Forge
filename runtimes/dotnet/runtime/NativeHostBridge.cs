using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using F4Forge.DotNet.Sdk;
using SdkModuleHandle = F4Forge.DotNet.Sdk.ModuleHandle;

namespace F4Forge.DotNet.Runtime;

internal unsafe sealed class NativeHostBridge : IPluginHostBridge, IDisposable
{
    private readonly NativeApi* _api;
    private readonly SdkModuleHandle _module;
    private readonly List<CallbackRegistration> _registrations = [];
    private readonly TaskCompletionSource<bool> _quiesced =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Action? _finalized;
    private bool _disposed;
    private int _finalizationStarted;

    public NativeHostBridge(NativeApi* api, ulong runtime, string id)
    {
        _api = api;
        if (api == null || api->RegisterModule == null) return;
        var bytes = Encoding.UTF8.GetBytes(id);
        fixed (byte* text = bytes)
        {
            ulong module = 0;
            var result = api->RegisterModule(runtime,
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, 1, &module);
            if (result != (int)F4ForgeResult.Success)
                throw new InvalidOperationException($"Native module registration failed: {result}");
            _module = new SdkModuleHandle(module);
        }
    }

    public SdkModuleHandle Module => _module;

    public EndpointHandle ResolveEndpoint(string name, uint version)
    {
        if (_api == null || _api->ResolveEndpoint == null) return default;
        var bytes = Encoding.UTF8.GetBytes(name);
        fixed (byte* text = bytes)
            return new EndpointHandle(_api->ResolveEndpoint(
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, version));
    }

    public F4ForgeResult Invoke(EndpointHandle endpoint, ReadOnlySpan<byte> request, Span<byte> response,
        out uint responseSize)
    {
        responseSize = 0;
        if (_api == null || _api->Invoke == null) return F4ForgeResult.InactiveRuntime;
        fixed (byte* requestPointer = request)
        fixed (byte* responsePointer = response)
        fixed (uint* size = &responseSize)
        {
            return (F4ForgeResult)_api->Invoke(endpoint.Value,
                request.IsEmpty ? null : requestPointer, (uint)request.Length,
                response.IsEmpty ? null : responsePointer, (uint)response.Length, size);
        }
    }

    public HostRegistration<EndpointHandle>? RegisterEndpoint(string name, EndpointKind kind, uint version,
        uint requestSize, uint responseSize, ThreadPolicy threadPolicy, EndpointCallback callback)
    {
        if (_api == null || _api->RegisterEndpoint == null || !_module.IsValid) return null;
        var state = new EndpointState(callback);
        var handle = RegisterEndpointNative(name, kind, version, requestSize, responseSize, threadPolicy, state);
        if (!handle.IsValid) { state.Dispose(); return null; }
        var resource = new CallbackRegistration(_api, handle.Value, state, CallbackKind.Endpoint);
        _registrations.Add(resource);
        return new HostRegistration<EndpointHandle> { Handle = handle, Resource = resource };
    }

    private EndpointHandle RegisterEndpointNative(string name, EndpointKind kind, uint version,
        uint requestSize, uint responseSize, ThreadPolicy threadPolicy, EndpointState state)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(state));
        fixed (byte* text = bytes)
        {
            var definition = new F4ForgeEndpointDefinition {
                StructSize = (uint)sizeof(F4ForgeEndpointDefinition), Kind = (uint)kind,
                Version = version, ThreadPolicy = (uint)threadPolicy,
                RequestSize = requestSize, ResponseSize = responseSize,
                Name = new F4ForgeStringView { Data = text, Length = (uint)bytes.Length },
                Thunk = &EndpointThunk, Context = (void*)context
            };
            ulong endpoint = 0;
            var result = _api->RegisterEndpoint(_module.Value, &definition, &endpoint);
            if (result != (int)F4ForgeResult.Success)
            {
                GCHandle.FromIntPtr(context).Free();
                return default;
            }
            state.Handle = context;
            return new EndpointHandle(endpoint);
        }
    }

    public HostSubscription<EventSubscriptionHandle>? Subscribe(EndpointHandle endpoint,
        Action<ReadOnlyMemory<byte>> callback)
    {
        if (_api == null || _api->Subscribe == null || !_module.IsValid) return null;
        var state = new EventState(callback);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(state));
        var handle = _api->Subscribe(_module.Value, endpoint.Value, &EventThunk, (void*)context);
        if (handle == 0) { GCHandle.FromIntPtr(context).Free(); return null; }
        state.Handle = context;
        var resource = new CallbackRegistration(_api, handle, state, CallbackKind.Event);
        _registrations.Add(resource);
        return new HostSubscription<EventSubscriptionHandle> {
            Handle = new EventSubscriptionHandle(handle), Resource = resource };
    }

    public HostSubscription<InterceptorSubscriptionHandle>? Intercept(EndpointHandle endpoint,
        Func<Memory<byte>, F4ForgeResult> callback)
    {
        if (_api == null || _api->Intercept == null || !_module.IsValid) return null;
        var state = new InterceptorState(callback);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(state));
        var handle = _api->Intercept(_module.Value, endpoint.Value, &InterceptorThunk, (void*)context);
        if (handle == 0) { GCHandle.FromIntPtr(context).Free(); return null; }
        state.Handle = context;
        var resource = new CallbackRegistration(_api, handle, state, CallbackKind.Interceptor);
        _registrations.Add(resource);
        return new HostSubscription<InterceptorSubscriptionHandle> {
            Handle = new InterceptorSubscriptionHandle(handle), Resource = resource };
    }

    public uint QueryCapability(string id, uint minimumVersion)
    {
        if (_api == null || _api->QueryCapability == null) return 0;
        var bytes = Encoding.UTF8.GetBytes(id);
        fixed (byte* text = bytes)
            return _api->QueryCapability(
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, minimumVersion);
    }

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
            return Task.FromResult(new AsyncOperationResult(
                created.Result, [], created.Result));
        var operation = created.Operation;
        return Task.Run(() => WaitAndCollect(operation, cancellationToken));
    }

    private (F4ForgeResult Result, ulong Operation) CreateOperation(
        EndpointHandle endpoint, byte[] data, bool emit)
    {
        ulong operation = 0;
        fixed (byte* bytes = data)
        {
            var result = emit
                ? _api->EmitAsync(_module.Value, endpoint.Value, bytes, (uint)data.Length, &operation)
                : _api->InvokeAsync(_module.Value, endpoint.Value, bytes, (uint)data.Length, &operation);
            return ((F4ForgeResult)result, operation);
        }
    }

    private AsyncOperationResult WaitAndCollect(ulong operation, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _api->CancelOperation(operation));
        var wait = (F4ForgeResult)_api->WaitOperation(operation, uint.MaxValue);
        var response = new byte[64 * 1024];
        uint responseSize = 0;
        var invocation = 0;
        F4ForgeResult get;
        fixed (byte* responsePointer = response)
        {
            var responseSizePointer = &responseSize;
            var invocationPointer = &invocation;
            get = (F4ForgeResult)_api->GetOperationResult(operation, invocationPointer,
                responsePointer, (uint)response.Length, responseSizePointer);
        }
        if (get == F4ForgeResult.BufferTooSmall) response = [];
        else if (get == F4ForgeResult.Success) Array.Resize(ref response, (int)responseSize);
        _api->ReleaseOperation(operation);
        return new(wait, response, (F4ForgeResult)invocation);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var registration in _registrations) registration.Dispose();
        if (_api == null || !_module.IsValid || _api->UnregisterModule == null) {
            FinalizeCallbackStates();
            return;
        }
        _api->UnregisterModule(_module.Value);
        if (!HasModuleQuiescenceApi) return;
        _ = Task.Run(() => {
            var quiescence = (F4ForgeResult)_api->WaitModuleQuiescence(
                _module.Value, uint.MaxValue);
            if (quiescence is F4ForgeResult.Success or F4ForgeResult.InvalidHandle)
                FinalizeCallbackStates();
        });
    }

    public bool IsQuiesced => _quiesced.Task.IsCompletedSuccessfully;

    private bool HasModuleQuiescenceApi =>
        _api != null && _api->StructSize >= 168 && _api->WaitModuleQuiescence != null;

    public void SetFinalizationCallback(Action callback)
    {
        _finalized = callback;
        if (IsQuiesced) callback();
    }

    private void FinalizeCallbackStates()
    {
        if (Interlocked.Exchange(ref _finalizationStarted, 1) != 0) return;
        foreach (var registration in _registrations) registration.Retire();
        _quiesced.TrySetResult(true);
        _finalized?.Invoke();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int EndpointThunk(void* context, void* request, uint requestSize, void* response,
        uint responseCapacity, uint* responseSize)
    {
        try
        {
            var state = (EndpointState)GCHandle.FromIntPtr((nint)context).Target!;
            var requestBytes = new byte[requestSize];
            if (requestSize != 0) Marshal.Copy((nint)request, requestBytes, 0, (int)requestSize);
            var responseBytes = new byte[responseCapacity];
            var result = state.Callback(requestBytes, responseBytes);
            if (responseSize != null) *responseSize = Math.Min(responseCapacity, (uint)responseBytes.Length);
            if (response != null && responseCapacity != 0)
                Marshal.Copy(responseBytes, 0, (nint)response, (int)responseCapacity);
            return (int)result;
        }
        catch { return (int)F4ForgeResult.InternalError; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void EventThunk(ulong subscription, ulong endpoint, void* payload, uint payloadSize, void* context)
    {
        try
        {
            var state = (EventState)GCHandle.FromIntPtr((nint)context).Target!;
            var bytes = new byte[payloadSize];
            if (payloadSize != 0) Marshal.Copy((nint)payload, bytes, 0, (int)payloadSize);
            state.Callback(bytes);
        }
        catch { }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int InterceptorThunk(ulong subscription, ulong endpoint, void* payload, uint payloadSize, void* context)
    {
        try
        {
            var state = (InterceptorState)GCHandle.FromIntPtr((nint)context).Target!;
            var bytes = new byte[payloadSize];
            if (payloadSize != 0) Marshal.Copy((nint)payload, bytes, 0, (int)payloadSize);
            var result = state.Callback(bytes);
            if (payload != null && payloadSize != 0) Marshal.Copy(bytes, 0, (nint)payload, (int)payloadSize);
            return (int)result;
        }
        catch { return (int)F4ForgeResult.InternalError; }
    }

    private sealed class EndpointState(EndpointCallback callback) : IDisposable
    {
        public EndpointCallback Callback { get; } = callback;
        public nint Handle { get; set; }
        public void Dispose()
        {
            if (Handle != 0) { GCHandle.FromIntPtr(Handle).Free(); Handle = 0; }
        }
    }

    private sealed class EventState(Action<ReadOnlyMemory<byte>> callback) : IDisposable
    {
        public Action<ReadOnlyMemory<byte>> Callback { get; } = callback;
        public nint Handle { get; set; }
        public void Dispose()
        {
            if (Handle != 0) { GCHandle.FromIntPtr(Handle).Free(); Handle = 0; }
        }
    }

    private sealed class InterceptorState(Func<Memory<byte>, F4ForgeResult> callback) : IDisposable
    {
        public Func<Memory<byte>, F4ForgeResult> Callback { get; } = callback;
        public nint Handle { get; set; }
        public void Dispose()
        {
            if (Handle != 0) { GCHandle.FromIntPtr(Handle).Free(); Handle = 0; }
        }
    }

    private enum CallbackKind { Endpoint, Event, Interceptor }

    private sealed class CallbackRegistration(
        NativeApi* api, ulong handle, IDisposable state, CallbackKind kind) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (kind == CallbackKind.Event) api->Unsubscribe(handle);
            else if (kind == CallbackKind.Interceptor) api->RemoveInterceptor(handle);
        }

        public void Retire()
        {
            Dispose();
            state.Dispose();
        }
    }
}
