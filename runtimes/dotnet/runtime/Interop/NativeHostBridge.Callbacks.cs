using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using F4Forge.DotNet.Sdk;
using SdkModuleHandle = F4Forge.DotNet.Sdk.ModuleHandle;

namespace F4Forge.DotNet.Runtime.Interop;

internal unsafe sealed partial class NativeHostBridge
{
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

    public IDisposable? SubscribeKeyDown(Action<F4ForgeKeyEventData> callback)
    {
        if (_api == null || _api->Subscribe == null || !_module.IsValid) return null;
        var endpoint = ResolveEndpoint("input.key.down", 1);
        if (!endpoint.IsValid) return null;
        var state = new KeyEventState(callback);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(state));
        var handle = _api->Subscribe(_module.Value, endpoint.Value, &KeyEventThunk, (void*)context);
        if (handle == 0) { GCHandle.FromIntPtr(context).Free(); return null; }
        state.Handle = context;
        var resource = new CallbackRegistration(_api, handle, state, CallbackKind.Key);
        _registrations.Add(resource);
        return resource;
    }

    public IDisposable? SubscribeFramework(string name, Action callback)
    {
        if (_api == null || _api->Subscribe == null || !_module.IsValid) return null;
        var endpoint = ResolveEndpoint(name, 1);
        if (!endpoint.IsValid) return null;
        var state = new FrameworkState(callback);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(state));
        var handle = _api->Subscribe(_module.Value, endpoint.Value, &FrameworkThunk, (void*)context);
        if (handle == 0) { GCHandle.FromIntPtr(context).Free(); return null; }
        state.Handle = context;
        var resource = new CallbackRegistration(_api, handle, state, CallbackKind.Framework);
        _registrations.Add(resource);
        return resource;
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
    private static void KeyEventThunk(ulong subscription, ulong endpoint, void* payload, uint payloadSize, void* context)
    {
        try
        {
            if (payload == null || payloadSize < (uint)sizeof(F4ForgeKeyEventData)) return;
            var state = (KeyEventState)GCHandle.FromIntPtr((nint)context).Target!;
            state.Callback(*(F4ForgeKeyEventData*)payload);
        }
        catch { }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void FrameworkThunk(ulong subscription, ulong endpoint, void* payload, uint payloadSize, void* context)
    {
        try
        {
            var state = (FrameworkState)GCHandle.FromIntPtr((nint)context).Target!;
            state.Callback();
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

    private sealed class KeyEventState(Action<F4ForgeKeyEventData> callback) : IDisposable
    {
        public Action<F4ForgeKeyEventData> Callback { get; } = callback;
        public nint Handle { get; set; }
        public void Dispose()
        {
            if (Handle != 0) { GCHandle.FromIntPtr(Handle).Free(); Handle = 0; }
        }
    }

    private sealed class FrameworkState(Action callback) : IDisposable
    {
        public Action Callback { get; } = callback;
        public nint Handle { get; set; }
        public void Dispose()
        {
            if (Handle != 0) { GCHandle.FromIntPtr(Handle).Free(); Handle = 0; }
        }
    }

    private enum CallbackKind { Endpoint, Event, Key, Framework, Interceptor }

    private sealed class CallbackRegistration(
        NativeApi* api, ulong handle, IDisposable state, CallbackKind kind) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (kind is CallbackKind.Event or CallbackKind.Key or CallbackKind.Framework) api->Unsubscribe(handle);
            else if (kind == CallbackKind.Interceptor) api->RemoveInterceptor(handle);
        }

        public void Retire()
        {
            Dispose();
            state.Dispose();
        }
    }
}
