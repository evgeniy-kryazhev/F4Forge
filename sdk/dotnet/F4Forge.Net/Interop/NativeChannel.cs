using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using F4Forge.Net.Events;
using F4Forge.Net.Messaging;
using F4Forge.Net.Serialization;
using F4Forge.Net.Diagnostics;

namespace F4Forge.Net.Interop;

internal sealed unsafe class NativeChannel : IMessageChannel, IEventChannel
{
    private const int MaximumMessageBytes = 1024 * 1024;
    private readonly ChannelBinding _binding;

    public NativeChannel(ChannelBinding binding, IDataSerializer? serializer = null)
    {
        if (binding.Context == 0 || binding.Call is null || binding.Add is null || binding.Remove is null
            || binding.Publish is null || binding.Subscribe is null || binding.Unsubscribe is null || binding.DataFormat is null)
        {
            throw new ArgumentException("Incomplete channel binding.", nameof(binding));
        }
        var format = Marshal.PtrToStringUTF8((nint)binding.DataFormat)!;
        Serializer = serializer ?? DataSerializers.Create(format);
        if (Serializer.Format != format)
        {
            throw new ArgumentException($"The host uses '{format}', but the SDK serializer uses '{Serializer.Format}'.", nameof(serializer));
        }
        _binding = binding;
    }

    public IDataSerializer Serializer { get; }

    public TResult Call<TResult>(string method, object? arguments)
    {
        ValidateName(method);
        var request = Serializer.Serialize(new MessageRequest(method, arguments));
        return Send<TResult>(request, publish: false);
    }

    public void Publish<T>(string name, T data)
    {
        ValidateName(name);
        var request = Serializer.Serialize(new EventRequest(name, data));
        Send<object?>(request, publish: true);
    }

    public IDisposable Register<TArguments, TResult>(string method, Func<TArguments, TResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterCallback(method, bytes =>
        {
            var arguments = Serializer.Deserialize<TArguments>(bytes);
            var result = handler(arguments);
            return Serializer.Serialize(new ProtocolResponse<TResult>(true, result));
        }, subscribe: false);
    }

    public IDisposable Subscribe<T>(string name, Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return RegisterCallback(name, bytes =>
        {
            var data = Serializer.Deserialize<T>(bytes);
            handler(data);
            return Serializer.Serialize(new ProtocolResponse<object?>(true));
        }, subscribe: true);
    }

    private TResult Send<TResult>(byte[] request, bool publish)
    {
        if (request.Length > MaximumMessageBytes)
        {
            throw new ArgumentException("Message exceeds the size limit.", nameof(request));
        }
        var response = new ResponseBuffer();
        var responseHandle = GCHandle.Alloc(response);
        try
        {
            fixed (byte* requestPointer = request)
            {
                var dispatch = publish ? _binding.Publish : _binding.Call;
                var status = dispatch(_binding.Context, requestPointer, (uint)request.Length,
                    &Receive, GCHandle.ToIntPtr(responseHandle));
                if (status != 0 || response.Bytes is null)
                {
                    throw new MessageException("transport_failed", "The native transport failed.");
                }
            }
            var envelope = Serializer.Deserialize<ProtocolResponse<TResult>>(response.Bytes);
            if (envelope is null || (!envelope.Success && envelope.Error is null))
            {
                throw new MessageException("invalid_response", "The host returned an invalid response envelope.");
            }
            if (envelope.Error is not null)
            {
                throw new MessageException(envelope.Error.Code, envelope.Error.Message);
            }
            return envelope.Value!;
        }
        finally
        {
            responseHandle.Free();
        }
    }

    private IDisposable RegisterCallback(string name, Func<byte[], byte[]> handler, bool subscribe)
    {
        ValidateName(name);
        var encodedName = Encoding.UTF8.GetBytes(name);
        var handle = GCHandle.Alloc(new Callback(Serializer, handler, DiagnosticTrace.CurrentOwner, name, subscribe,
            entry => Call<bool>("diagnostics.record", entry)));
        try
        {
            fixed (byte* namePointer = encodedName)
            {
                var add = subscribe ? _binding.Subscribe : _binding.Add;
                var registration = add(_binding.Context, namePointer, (uint)encodedName.Length, &Invoke, GCHandle.ToIntPtr(handle));
                if (registration == 0)
                {
                    throw new InvalidOperationException($"Cannot register '{name}'; it may already exist.");
                }
                var remove = subscribe ? _binding.Unsubscribe : _binding.Remove;
                return new Registration(_binding.Context, remove, registration, handle);
            }
        }
        catch
        {
            handle.Free();
            throw;
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Encoding.UTF8.GetByteCount(name) > 256)
        {
            throw new ArgumentException("Names must fit in 256 UTF-8 bytes.", nameof(name));
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Receive(nint context, byte* data, uint length)
    {
        try
        {
            if (data is null || length > MaximumMessageBytes)
            {
                return 1;
            }
            var response = (ResponseBuffer)GCHandle.FromIntPtr(context).Target!;
            response.Bytes = new ReadOnlySpan<byte>(data, (int)length).ToArray();
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Invoke(nint context, byte* data, uint length,
        delegate* unmanaged[Cdecl]<nint, byte*, uint, int> receive, nint receiver)
    {
        try
        {
            if (data is null || length > MaximumMessageBytes || receive is null)
            {
                return 1;
            }
            // This local root keeps a self-unsubscribing callback alive until it returns.
            var callback = (Callback)GCHandle.FromIntPtr(context).Target!;
            byte[] response;
            try
            {
                var payload = new ReadOnlySpan<byte>(data, (int)length).ToArray();
                response = DiagnosticTrace.Run(callback.ModId, callback.Subscribe ? "event.handle" : "message.handle",
                    callback.Name, callback.Record, () => callback.Handler(payload));
            }
            catch (Exception exception)
            {
                var error = new ProtocolError("handler_failed", exception.Message);
                response = callback.Serializer.Serialize(new ProtocolResponse<object?>(false, Error: error));
            }
            if (response.Length > MaximumMessageBytes)
            {
                return 1;
            }
            fixed (byte* responsePointer = response)
            {
                return receive(receiver, responsePointer, (uint)response.Length);
            }
        }
        catch
        {
            // Serializer failures also stay on the managed side of this boundary.
            return 1;
        }
    }

    private sealed record Callback(IDataSerializer Serializer, Func<byte[], byte[]> Handler,
        string? ModId, string Name, bool Subscribe, Action<DiagnosticRecord> Record);

    private sealed class ResponseBuffer
    {
        public byte[]? Bytes { get; set; }
    }

    private sealed class Registration(nint context, delegate* unmanaged[Cdecl]<nint, ulong, void> remove,
        ulong identifier, GCHandle handler) : IDisposable
    {
        private int _freed;

        public void Dispose()
        {
            // Every disposer joins native quiescence, including concurrent disposers.
            // A callback disposing itself is already rooted by Invoke's local state.
            remove(context, identifier);
            if (Interlocked.Exchange(ref _freed, 1) == 0)
            {
                handler.Free();
            }
        }
    }
}
