namespace F4Forge.DotNet.Sdk;

public enum InputDevice : uint { Keyboard = 0, Mouse = 1, Gamepad = 2 }

public enum Key : uint
{
    Unknown = 0,
    Backspace = 0x08, Tab = 0x09, Enter = 0x0D, Pause = 0x13,
    CapsLock = 0x14, Escape = 0x1B, Space = 0x20, PageUp = 0x21,
    PageDown = 0x22, End = 0x23, Home = 0x24, Left = 0x25, Up = 0x26,
    Right = 0x27, Down = 0x28, PrintScreen = 0x2C, Insert = 0x2D, Delete = 0x2E,
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
    D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46,
    G = 0x47, H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C,
    M = 0x4D, N = 0x4E, O = 0x4F, P = 0x50, Q = 0x51, R = 0x52,
    S = 0x53, T = 0x54, U = 0x55, V = 0x56, W = 0x57, X = 0x58,
    Y = 0x59, Z = 0x5A, Apps = 0x5D,
    Numpad0 = 0x60, Numpad1 = 0x61, Numpad2 = 0x62, Numpad3 = 0x63,
    Numpad4 = 0x64, Numpad5 = 0x65, Numpad6 = 0x66, Numpad7 = 0x67,
    Numpad8 = 0x68, Numpad9 = 0x69, NumpadMultiply = 0x6A, NumpadPlus = 0x6B,
    NumpadMinus = 0x6D, NumpadPeriod = 0x6E, NumpadDivide = 0x6F,
    F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
    F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,
    NumLock = 0x90, ScrollLock = 0x91, LShift = 0xA0, RShift = 0xA1,
    LControl = 0xA2, RControl = 0xA3, LAlt = 0xA4, RAlt = 0xA5,
    Semicolon = 0xBA, Equals = 0xBB, Comma = 0xBC, Minus = 0xBD,
    Period = 0xBE, Slash = 0xBF, LBracket = 0xDB, Backslash = 0xDC,
    RBracket = 0xDD, Apostrophe = 0xDE
}

#pragma warning disable CA1711
public sealed class KeyDownEventArgs
{
    internal KeyDownEventArgs(InputDevice device, Key key, bool isRepeat, float heldSeconds, bool isMenu)
    {
        Device = device;
        Key = key;
        IsRepeat = isRepeat;
        HeldSeconds = heldSeconds;
        IsMenu = isMenu;
    }

    public InputDevice Device { get; internal set; }
    public Key Key { get; internal set; }
    public bool IsRepeat { get; internal set; }
    public float HeldSeconds { get; internal set; }
    public bool IsMenu { get; internal set; }
    public override string ToString() => $"{Device}/{Key} (repeat={IsRepeat})";
}
#pragma warning restore CA1711

public delegate void KeyDownHandler(KeyDownEventArgs args);
