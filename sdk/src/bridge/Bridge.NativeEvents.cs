using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace F4Forge.Bridge;

public static partial class Bridge
{
    private static Action<string>? _reportNativeEventError;

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct EventSnapshot
    {
        public ulong* Fields;
        public int Count;
    }

    internal static unsafe void ConnectNativeEvents(Action<string> reportError)
    {
        _reportNativeEventError = reportError;
        SubscribeEvent(&OnNativeEvent);
    }

    [LibraryImport("F4Forge.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void SubscribeEvent(delegate* unmanaged[Cdecl]<byte*, nint, F4SEValue*, int> callback);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int OnNativeEvent(byte* namePointer, nint senderPointer, F4SEValue* args)
    {
        try
        {
            if (namePointer is null || args is null)
            {
                throw new ArgumentException("A native event requires a name and a payload.");
            }
            var name = Marshal.PtrToStringUTF8((nint)namePointer)!;
            object? sender = senderPointer == 0 ? null : (object)senderPointer;
            switch (args->Type)
            {
                case ValueType.EventSnapshot:
                    var snapshot = (EventSnapshot*)args->Pointer;
                    if (snapshot is null || snapshot->Count < 0 || (snapshot->Count > 0 && snapshot->Fields is null))
                    {
                        throw new ArgumentException($"Invalid snapshot for native event '{name}'.");
                    }
                    var fields = new ReadOnlySpan<ulong>(snapshot->Fields, snapshot->Count);
                    PublishCommonLibEvent(name, sender, fields);
                    break;
                case ValueType.Int32:
                    Publish(name, sender, args->Integer);
                    break;
                case ValueType.String:
                    var text = Marshal.PtrToStringUTF8(args->Pointer)
                        ?? throw new ArgumentException($"Missing UTF-8 string for native event '{name}'.");
                    Publish(name, sender, text);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported payload type {args->Type} for native event '{name}'.");
            }
            return 1;
        }
        catch (Exception exception)
        {
            // Keep failures from both handlers and diagnostics on the managed side of the callback.
            try
            {
                _reportNativeEventError?.Invoke(exception.ToString());
            }
            catch (Exception)
            {
                return 0;
            }
            return 0;
        }
    }
}
