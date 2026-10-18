using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using F4Forge.Net.Messaging;

namespace F4Forge.Net.Interop;

internal sealed unsafe class NativeChannel(ChannelBinding binding) : IMessageChannel
{
    private const int MaximumMessageBytes = 1024 * 1024;

    public TResult Call<TResult>(string method, object? arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        var request = JsonSerializer.SerializeToUtf8Bytes(new { method, arguments });
        if (request.Length > MaximumMessageBytes)
        {
            throw new ArgumentException("Message exceeds the size limit.", nameof(arguments));
        }
        var response = new ResponseBuffer();
        var responseHandle = GCHandle.Alloc(response);
        try
        {
            fixed (byte* requestPointer = request)
            {
                var status = binding.Call(binding.Context, requestPointer, (uint)request.Length,
                    &Receive, GCHandle.ToIntPtr(responseHandle));
                if (status != 0 || response.Bytes is null)
                {
                    throw new MessageException("transport_failed", "The native transport failed.");
                }
            }
            using var document = JsonDocument.Parse(response.Bytes);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                throw new MessageException(error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
            }
            return document.RootElement.GetProperty("value").Deserialize<TResult>()!;
        }
        finally
        {
            responseHandle.Free();
        }
    }

    public IDisposable Register(string method, Func<JsonElement, object?> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(handler);
        var name = Encoding.UTF8.GetBytes(method);
        var handle = GCHandle.Alloc(handler);
        try
        {
            fixed (byte* namePointer = name)
            {
                var registration = binding.Add(binding.Context, namePointer, (uint)name.Length, &Invoke, GCHandle.ToIntPtr(handle));
                if (registration == 0)
                {
                    throw new InvalidOperationException($"Cannot register method '{method}'; it may already exist.");
                }
                return new Registration(binding, registration, handle);
            }
        }
        catch
        {
            handle.Free();
            throw;
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
            byte[] response;
            try
            {
                var handler = (Func<JsonElement, object?>)GCHandle.FromIntPtr(context).Target!;
                using var arguments = JsonDocument.Parse(new ReadOnlySpan<byte>(data, (int)length).ToArray());
                response = JsonSerializer.SerializeToUtf8Bytes(new { value = handler(arguments.RootElement) });
            }
            catch (Exception exception)
            {
                response = JsonSerializer.SerializeToUtf8Bytes(new { error = new { message = exception.Message } });
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
            // Exceptions must never unwind through a native function pointer.
            return 1;
        }
    }

    private sealed class ResponseBuffer
    {
        public byte[]? Bytes { get; set; }
    }

    private sealed class Registration(ChannelBinding host, ulong identifier, GCHandle handler) : IDisposable
    {
        private long _identifier = checked((long)identifier);

        public void Dispose()
        {
            var identifierToRemove = Interlocked.Exchange(ref _identifier, 0);
            if (identifierToRemove == 0)
            {
                return;
            }
            // Native removal waits for active callbacks before their GC root is freed.
            host.Remove(host.Context, (ulong)identifierToRemove);
            handler.Free();
        }
    }
}
