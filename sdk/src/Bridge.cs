using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace F4Forge;

/// <summary>Calls named native operations provided by the F4Forge host.</summary>
public static partial class Bridge
{
    private enum ValueType : int
    {
        Error = -1,
        Int32 = 2
    }

    private enum InvokeError : int
    {
        UnknownOperation = 1,
        InvalidArguments = 2,
        Overflow = 3,
        RegistryUnavailable = 4
    }

    // Mirrors F4SEValue in native/src/bridge/channel.h.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct F4SEValue
    {
        [FieldOffset(0)] public ValueType Type;
        [FieldOffset(8)] public int Integer;
        [FieldOffset(8)] public InvokeError Error;
    }

    /// <summary>Invokes an operation with signed 32-bit integer arguments.</summary>
    /// <typeparam name="T">The result type. The prototype supports <see cref="int"/>.</typeparam>
    public static unsafe T Invoke<T>(string name, params ReadOnlySpan<int> arguments) where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Contains('\0'))
        {
            throw new ArgumentException("An operation name cannot contain a null character.", nameof(name));
        }
        if (typeof(T) != typeof(int))
        {
            throw new NotSupportedException($"The bridge does not support results of type {typeof(T)}.");
        }

        const int maxStackArguments = 32;
        Span<F4SEValue> values = arguments.Length <= maxStackArguments
            ? stackalloc F4SEValue[arguments.Length]
            : new F4SEValue[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            values[index] = new F4SEValue { Type = ValueType.Int32, Integer = arguments[index] };
        }

        F4SEValue result = default;
        fixed (F4SEValue* argumentPointer = values)
        {
            InvokeFFI(name, argumentPointer, values.Length, &result);
        }
        if (result.Type == ValueType.Error)
        {
            throw result.Error switch
            {
                InvokeError.UnknownOperation => new MissingMethodException($"Unknown native operation '{name}'."),
                InvokeError.InvalidArguments => new ArgumentException($"Invalid arguments for '{name}'.", nameof(arguments)),
                InvokeError.Overflow => new OverflowException($"The result of '{name}' exceeds the Int32 range."),
                InvokeError.RegistryUnavailable => new InvalidOperationException("The native bridge method registry is unavailable."),
                _ => new InvalidOperationException($"Unexpected native error {(int)result.Error} from '{name}'.")
            };
        }
        if (result.Type != ValueType.Int32)
        {
            throw new InvalidOperationException($"'{name}' returned an unexpected result type: {result.Type}.");
        }
        return Unsafe.As<int, T>(ref result.Integer);
    }

    [LibraryImport("F4Forge.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void InvokeFFI(string name, F4SEValue* arguments, int count, F4SEValue* result);
}
