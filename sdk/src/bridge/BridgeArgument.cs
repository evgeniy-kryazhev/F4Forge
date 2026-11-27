using System.Runtime.InteropServices;

namespace F4Forge;

/// <summary>A tagged scalar or borrowed native address passed to a bridge operation.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct BridgeArgument
{
    private readonly Bridge.F4SEValue _value;

    private BridgeArgument(Bridge.F4SEValue value) => _value = value;

    /// <summary>Pass a signed 32-bit integer.</summary>
    public static implicit operator BridgeArgument(int value) =>
        new(new Bridge.F4SEValue { Type = Bridge.ValueType.Int32, Integer = value });

    /// <summary>Pass an unsigned 32-bit integer.</summary>
    public static implicit operator BridgeArgument(uint value) =>
        new(new Bridge.F4SEValue { Type = Bridge.ValueType.UInt32, UnsignedInteger = value });

    /// <summary>Pass a single-precision floating point value.</summary>
    public static implicit operator BridgeArgument(float value) =>
        new(new Bridge.F4SEValue { Type = Bridge.ValueType.Float, Number = value });

    /// <summary>Pass a Boolean value.</summary>
    public static implicit operator BridgeArgument(bool value) =>
        new(new Bridge.F4SEValue { Type = Bridge.ValueType.Bool, Boolean = value ? (byte)1 : (byte)0 });

    /// <summary>Pass the address of an object of the exact native type required by the binding.</summary>
    public static implicit operator BridgeArgument(nint value) =>
        new(new Bridge.F4SEValue { Type = Bridge.ValueType.Pointer, Pointer = value });
}
