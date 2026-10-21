namespace F4Forge.Net.Input.Events;

/// <summary>Engine input codes for keyboard, mouse and gamepad buttons.</summary>
/// <remarks>
/// Keyboard values are Windows virtual-key codes used by Fallout 4 1.11.240.
/// OEM punctuation depends on keyboard layout. Enter and numpad Enter share a code.
/// Mouse codes are 256–263 and gamepad codes are 266–281. Unnamed numeric codes
/// are preserved when received from the engine. Mouse wheel codes are excluded.
/// </remarks>
public enum KeyCode : uint
{
    /// <summary>The Backspace keyboard key.</summary>
    Backspace = 0x08,

    /// <summary>The Tab keyboard key.</summary>
    Tab = 0x09,

    /// <summary>The Enter keyboard key.</summary>
    Enter = 0x0D,

    /// <summary>The Pause keyboard key.</summary>
    Pause = 0x13,

    /// <summary>The CapsLock keyboard key.</summary>
    CapsLock = 0x14,

    /// <summary>The Kana keyboard key.</summary>
    Kana = 0x15,

    /// <summary>The Kanji keyboard key.</summary>
    Kanji = 0x19,

    /// <summary>The Escape keyboard key.</summary>
    Escape = 0x1B,

    /// <summary>The Convert keyboard key.</summary>
    Convert = 0x1C,

    /// <summary>The NoConvert keyboard key.</summary>
    NoConvert = 0x1D,

    /// <summary>The Space keyboard key.</summary>
    Space = 0x20,

    /// <summary>The PageUp keyboard key.</summary>
    PageUp = 0x21,

    /// <summary>The PageDown keyboard key.</summary>
    PageDown = 0x22,

    /// <summary>The End keyboard key.</summary>
    End = 0x23,

    /// <summary>The Home keyboard key.</summary>
    Home = 0x24,

    /// <summary>The Left keyboard key.</summary>
    Left = 0x25,

    /// <summary>The Up keyboard key.</summary>
    Up = 0x26,

    /// <summary>The Right keyboard key.</summary>
    Right = 0x27,

    /// <summary>The Down keyboard key.</summary>
    Down = 0x28,

    /// <summary>The PrintScreen keyboard key.</summary>
    PrintScreen = 0x2C,

    /// <summary>The Insert keyboard key.</summary>
    Insert = 0x2D,

    /// <summary>The Delete keyboard key.</summary>
    Delete = 0x2E,

    /// <summary>The D0 keyboard key.</summary>
    D0 = 0x30,

    /// <summary>The D1 keyboard key.</summary>
    D1 = 0x31,

    /// <summary>The D2 keyboard key.</summary>
    D2 = 0x32,

    /// <summary>The D3 keyboard key.</summary>
    D3 = 0x33,

    /// <summary>The D4 keyboard key.</summary>
    D4 = 0x34,

    /// <summary>The D5 keyboard key.</summary>
    D5 = 0x35,

    /// <summary>The D6 keyboard key.</summary>
    D6 = 0x36,

    /// <summary>The D7 keyboard key.</summary>
    D7 = 0x37,

    /// <summary>The D8 keyboard key.</summary>
    D8 = 0x38,

    /// <summary>The D9 keyboard key.</summary>
    D9 = 0x39,

    /// <summary>The A keyboard key.</summary>
    A = 0x41,

    /// <summary>The B keyboard key.</summary>
    B = 0x42,

    /// <summary>The C keyboard key.</summary>
    C = 0x43,

    /// <summary>The D keyboard key.</summary>
    D = 0x44,

    /// <summary>The E keyboard key.</summary>
    E = 0x45,

    /// <summary>The F keyboard key.</summary>
    F = 0x46,

    /// <summary>The G keyboard key.</summary>
    G = 0x47,

    /// <summary>The H keyboard key.</summary>
    H = 0x48,

    /// <summary>The I keyboard key.</summary>
    I = 0x49,

    /// <summary>The J keyboard key.</summary>
    J = 0x4A,

    /// <summary>The K keyboard key.</summary>
    K = 0x4B,

    /// <summary>The L keyboard key.</summary>
    L = 0x4C,

    /// <summary>The M keyboard key.</summary>
    M = 0x4D,

    /// <summary>The N keyboard key.</summary>
    N = 0x4E,

    /// <summary>The O keyboard key.</summary>
    O = 0x4F,

    /// <summary>The P keyboard key.</summary>
    P = 0x50,

    /// <summary>The Q keyboard key.</summary>
    Q = 0x51,

    /// <summary>The R keyboard key.</summary>
    R = 0x52,

    /// <summary>The S keyboard key.</summary>
    S = 0x53,

    /// <summary>The T keyboard key.</summary>
    T = 0x54,

    /// <summary>The U keyboard key.</summary>
    U = 0x55,

    /// <summary>The V keyboard key.</summary>
    V = 0x56,

    /// <summary>The W keyboard key.</summary>
    W = 0x57,

    /// <summary>The X keyboard key.</summary>
    X = 0x58,

    /// <summary>The Y keyboard key.</summary>
    Y = 0x59,

    /// <summary>The Z keyboard key.</summary>
    Z = 0x5A,

    /// <summary>The LeftWindows keyboard key.</summary>
    LeftWindows = 0x5B,

    /// <summary>The RightWindows keyboard key.</summary>
    RightWindows = 0x5C,

    /// <summary>The Applications keyboard key.</summary>
    Applications = 0x5D,

    /// <summary>The Sleep keyboard key.</summary>
    Sleep = 0x5F,

    /// <summary>The Numpad0 keyboard key.</summary>
    Numpad0 = 0x60,

    /// <summary>The Numpad1 keyboard key.</summary>
    Numpad1 = 0x61,

    /// <summary>The Numpad2 keyboard key.</summary>
    Numpad2 = 0x62,

    /// <summary>The Numpad3 keyboard key.</summary>
    Numpad3 = 0x63,

    /// <summary>The Numpad4 keyboard key.</summary>
    Numpad4 = 0x64,

    /// <summary>The Numpad5 keyboard key.</summary>
    Numpad5 = 0x65,

    /// <summary>The Numpad6 keyboard key.</summary>
    Numpad6 = 0x66,

    /// <summary>The Numpad7 keyboard key.</summary>
    Numpad7 = 0x67,

    /// <summary>The Numpad8 keyboard key.</summary>
    Numpad8 = 0x68,

    /// <summary>The Numpad9 keyboard key.</summary>
    Numpad9 = 0x69,

    /// <summary>The NumpadMultiply keyboard key.</summary>
    NumpadMultiply = 0x6A,

    /// <summary>The NumpadAdd keyboard key.</summary>
    NumpadAdd = 0x6B,

    /// <summary>The NumpadComma keyboard key.</summary>
    NumpadComma = 0x6C,

    /// <summary>The NumpadSubtract keyboard key.</summary>
    NumpadSubtract = 0x6D,

    /// <summary>The NumpadDecimal keyboard key.</summary>
    NumpadDecimal = 0x6E,

    /// <summary>The NumpadDivide keyboard key.</summary>
    NumpadDivide = 0x6F,

    /// <summary>The F1 keyboard key.</summary>
    F1 = 0x70,

    /// <summary>The F2 keyboard key.</summary>
    F2 = 0x71,

    /// <summary>The F3 keyboard key.</summary>
    F3 = 0x72,

    /// <summary>The F4 keyboard key.</summary>
    F4 = 0x73,

    /// <summary>The F5 keyboard key.</summary>
    F5 = 0x74,

    /// <summary>The F6 keyboard key.</summary>
    F6 = 0x75,

    /// <summary>The F7 keyboard key.</summary>
    F7 = 0x76,

    /// <summary>The F8 keyboard key.</summary>
    F8 = 0x77,

    /// <summary>The F9 keyboard key.</summary>
    F9 = 0x78,

    /// <summary>The F10 keyboard key.</summary>
    F10 = 0x79,

    /// <summary>The F11 keyboard key.</summary>
    F11 = 0x7A,

    /// <summary>The F12 keyboard key.</summary>
    F12 = 0x7B,

    /// <summary>The F13 keyboard key.</summary>
    F13 = 0x7C,

    /// <summary>The F14 keyboard key.</summary>
    F14 = 0x7D,

    /// <summary>The F15 keyboard key.</summary>
    F15 = 0x7E,

    /// <summary>The F16 keyboard key.</summary>
    F16 = 0x7F,

    /// <summary>The F17 keyboard key.</summary>
    F17 = 0x80,

    /// <summary>The F18 keyboard key.</summary>
    F18 = 0x81,

    /// <summary>The F19 keyboard key.</summary>
    F19 = 0x82,

    /// <summary>The F20 keyboard key.</summary>
    F20 = 0x83,

    /// <summary>The F21 keyboard key.</summary>
    F21 = 0x84,

    /// <summary>The F22 keyboard key.</summary>
    F22 = 0x85,

    /// <summary>The F23 keyboard key.</summary>
    F23 = 0x86,

    /// <summary>The F24 keyboard key.</summary>
    F24 = 0x87,

    /// <summary>The NumLock keyboard key.</summary>
    NumLock = 0x90,

    /// <summary>The ScrollLock keyboard key.</summary>
    ScrollLock = 0x91,

    /// <summary>The LeftShift keyboard key.</summary>
    LeftShift = 0xA0,

    /// <summary>The RightShift keyboard key.</summary>
    RightShift = 0xA1,

    /// <summary>The LeftControl keyboard key.</summary>
    LeftControl = 0xA2,

    /// <summary>The RightControl keyboard key.</summary>
    RightControl = 0xA3,

    /// <summary>The LeftAlt keyboard key.</summary>
    LeftAlt = 0xA4,

    /// <summary>The RightAlt keyboard key.</summary>
    RightAlt = 0xA5,

    /// <summary>The WebBack keyboard key.</summary>
    WebBack = 0xA6,

    /// <summary>The WebForward keyboard key.</summary>
    WebForward = 0xA7,

    /// <summary>The WebRefresh keyboard key.</summary>
    WebRefresh = 0xA8,

    /// <summary>The WebStop keyboard key.</summary>
    WebStop = 0xA9,

    /// <summary>The WebSearch keyboard key.</summary>
    WebSearch = 0xAA,

    /// <summary>The WebFavorites keyboard key.</summary>
    WebFavorites = 0xAB,

    /// <summary>The WebHome keyboard key.</summary>
    WebHome = 0xAC,

    /// <summary>The Mute keyboard key.</summary>
    Mute = 0xAD,

    /// <summary>The VolumeDown keyboard key.</summary>
    VolumeDown = 0xAE,

    /// <summary>The VolumeUp keyboard key.</summary>
    VolumeUp = 0xAF,

    /// <summary>The NextTrack keyboard key.</summary>
    NextTrack = 0xB0,

    /// <summary>The PreviousTrack keyboard key.</summary>
    PreviousTrack = 0xB1,

    /// <summary>The MediaStop keyboard key.</summary>
    MediaStop = 0xB2,

    /// <summary>The PlayPause keyboard key.</summary>
    PlayPause = 0xB3,

    /// <summary>The Mail keyboard key.</summary>
    Mail = 0xB4,

    /// <summary>The MediaSelect keyboard key.</summary>
    MediaSelect = 0xB5,

    /// <summary>The Semicolon keyboard key.</summary>
    Semicolon = 0xBA,

    /// <summary>The Equals keyboard key.</summary>
    Equals = 0xBB,

    /// <summary>The Comma keyboard key.</summary>
    Comma = 0xBC,

    /// <summary>The Minus keyboard key.</summary>
    Minus = 0xBD,

    /// <summary>The Period keyboard key.</summary>
    Period = 0xBE,

    /// <summary>The Slash keyboard key.</summary>
    Slash = 0xBF,

    /// <summary>The Grave keyboard key.</summary>
    Grave = 0xC0,

    /// <summary>The LeftBracket keyboard key.</summary>
    LeftBracket = 0xDB,

    /// <summary>The Backslash keyboard key.</summary>
    Backslash = 0xDC,

    /// <summary>The RightBracket keyboard key.</summary>
    RightBracket = 0xDD,

    /// <summary>The Apostrophe keyboard key.</summary>
    Apostrophe = 0xDE,

    /// <summary>The Oem102 keyboard key.</summary>
    Oem102 = 0xE2,

    /// <summary>The left mouse button.</summary>
    MouseLeft = 256,

    /// <summary>The right mouse button.</summary>
    MouseRight = 257,

    /// <summary>The middle mouse button.</summary>
    MouseMiddle = 258,

    /// <summary>Mouse button 4.</summary>
    MouseButton4 = 259,

    /// <summary>Mouse button 5.</summary>
    MouseButton5 = 260,

    /// <summary>Mouse button 6.</summary>
    MouseButton6 = 261,

    /// <summary>Mouse button 7.</summary>
    MouseButton7 = 262,

    /// <summary>Mouse button 8.</summary>
    MouseButton8 = 263,

    /// <summary>The DPad Up gamepad button.</summary>
    GamepadDPadUp = 266,

    /// <summary>The DPad Down gamepad button.</summary>
    GamepadDPadDown = 267,

    /// <summary>The DPad Left gamepad button.</summary>
    GamepadDPadLeft = 268,

    /// <summary>The DPad Right gamepad button.</summary>
    GamepadDPadRight = 269,

    /// <summary>The Start gamepad button.</summary>
    GamepadStart = 270,

    /// <summary>The Back gamepad button.</summary>
    GamepadBack = 271,

    /// <summary>The Left Thumb gamepad button.</summary>
    GamepadLeftThumb = 272,

    /// <summary>The Right Thumb gamepad button.</summary>
    GamepadRightThumb = 273,

    /// <summary>The Left Shoulder gamepad button.</summary>
    GamepadLeftShoulder = 274,

    /// <summary>The Right Shoulder gamepad button.</summary>
    GamepadRightShoulder = 275,

    /// <summary>The A gamepad button.</summary>
    GamepadA = 276,

    /// <summary>The B gamepad button.</summary>
    GamepadB = 277,

    /// <summary>The X gamepad button.</summary>
    GamepadX = 278,

    /// <summary>The Y gamepad button.</summary>
    GamepadY = 279,

    /// <summary>The Left Trigger gamepad button.</summary>
    GamepadLeftTrigger = 280,

    /// <summary>The Right Trigger gamepad button.</summary>
    GamepadRightTrigger = 281
}
