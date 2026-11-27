using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace F4Forge;

/// <summary>Calls named native operations provided by the F4Forge host.</summary>
public static partial class Bridge
{
    internal enum ValueType : int
    {
        Error = -1,
        Void = 0,
        Bool = 1,
        Int32 = 2,
        UInt32 = 3,
        Float = 4,
        Pointer = 5,
        String = 6,
        EventSnapshot = 7
    }

    internal enum InvokeError : int
    {
        UnknownOperation = 1,
        InvalidArguments = 2,
        Overflow = 3,
        RegistryUnavailable = 4,
        NativeException = 5
    }

    // Mirrors F4SEValue in native/src/bridge/channel.h.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct F4SEValue
    {
        [FieldOffset(0)] public ValueType Type;
        [FieldOffset(8)] public int Integer;
        [FieldOffset(8)] public byte Boolean;
        [FieldOffset(8)] public uint UnsignedInteger;
        [FieldOffset(8)] public float Number;
        [FieldOffset(8)] public ulong Payload;
        [FieldOffset(8)] public nint Pointer;
        [FieldOffset(8)] public InvokeError Error;
    }

    /// <summary>Invokes an operation with signed 32-bit integer arguments.</summary>
    public static T Invoke<T>(string name, params ReadOnlySpan<int> arguments) where T : unmanaged
    {
        const int maxStackArguments = 32;
        Span<BridgeArgument> values = arguments.Length <= maxStackArguments
            ? stackalloc BridgeArgument[arguments.Length]
            : new BridgeArgument[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            values[index] = arguments[index];
        }
        return Invoke<T>(name, values);
    }

    /// <summary>Invokes a parameterless operation.</summary>
    public static T Invoke<T>(string name) where T : unmanaged => Invoke<T>(name, ReadOnlySpan<BridgeArgument>.Empty);

    /// <summary>Invokes a named operation with typed arguments. Native object addresses are borrowed.</summary>
    public static T Invoke<T>(string name, params ReadOnlySpan<BridgeArgument> arguments) where T : unmanaged
    {
        var result = InvokeCore(name, arguments, ResultType<T>());
        return Unsafe.As<ulong, T>(ref result.Payload);
    }

    private static ValueType ResultType<T>() where T : unmanaged
    {
        if (typeof(T) == typeof(int))
        {
            return ValueType.Int32;
        }
        if (typeof(T) == typeof(uint))
        {
            return ValueType.UInt32;
        }
        if (typeof(T) == typeof(float))
        {
            return ValueType.Float;
        }
        if (typeof(T) == typeof(bool))
        {
            return ValueType.Bool;
        }
        if (typeof(T) == typeof(nint))
        {
            return ValueType.Pointer;
        }
        throw new NotSupportedException($"Unsupported bridge result type {typeof(T)}.");
    }

    /// <summary>Invokes an operation returning void.</summary>
    public static void InvokeVoid(string name, params ReadOnlySpan<BridgeArgument> arguments)
    {
        InvokeCore(name, arguments, ValueType.Void);
    }

    private static unsafe F4SEValue InvokeCore(string name, ReadOnlySpan<BridgeArgument> arguments, ValueType expected)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Contains('\0'))
        {
            throw new ArgumentException("An operation name cannot contain a null character.", nameof(name));
        }
        F4SEValue result = default;
        fixed (BridgeArgument* argumentPointer = arguments)
        {
            InvokeFFI(name, (F4SEValue*)argumentPointer, arguments.Length, &result);
        }
        if (result.Type == ValueType.Error)
        {
            throw result.Error switch
            {
                InvokeError.UnknownOperation => new MissingMethodException($"Unknown native operation '{name}'."),
                InvokeError.InvalidArguments => new ArgumentException($"Invalid arguments for '{name}'.", nameof(arguments)),
                InvokeError.Overflow => new OverflowException($"The result of '{name}' exceeds the Int32 range."),
                InvokeError.RegistryUnavailable => new InvalidOperationException("The native bridge method registry is unavailable."),
                InvokeError.NativeException => new InvalidOperationException($"Native method '{name}' threw an exception."),
                _ => new InvalidOperationException($"Unexpected native error {(int)result.Error} from '{name}'.")
            };
        }
        if (result.Type != expected)
        {
            throw new InvalidOperationException($"'{name}' returned an unexpected result type: {result.Type}.");
        }
        return result;
    }

    [LibraryImport("F4Forge.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial void InvokeFFI(string name, F4SEValue* arguments, int count, F4SEValue* result);
}
