namespace F4Forge.Net.Input.Events;

/// <summary>F4SE input codes for keyboard, mouse and gamepad buttons.</summary>
/// <remarks>
/// Keyboard values are DirectInput scan codes, independent of the current text layout.
/// Mouse codes are 256–263 and gamepad codes are 266–281. Unnamed numeric codes
/// are preserved when received from the engine. Mouse wheel codes are excluded.
/// </remarks>
public enum KeyCode : uint
{
    /// <summary>The Escape keyboard key.</summary>
    Escape = 0x01,

    /// <summary>The D1 keyboard key.</summary>
    D1 = 0x02,

    /// <summary>The D2 keyboard key.</summary>
    D2 = 0x03,

    /// <summary>The D3 keyboard key.</summary>
    D3 = 0x04,

    /// <summary>The D4 keyboard key.</summary>
    D4 = 0x05,

    /// <summary>The D5 keyboard key.</summary>
    D5 = 0x06,

    /// <summary>The D6 keyboard key.</summary>
    D6 = 0x07,

    /// <summary>The D7 keyboard key.</summary>
    D7 = 0x08,

    /// <summary>The D8 keyboard key.</summary>
    D8 = 0x09,

    /// <summary>The D9 keyboard key.</summary>
    D9 = 0x0A,

    /// <summary>The D0 keyboard key.</summary>
    D0 = 0x0B,

    /// <summary>The Minus keyboard key.</summary>
    Minus = 0x0C,

    /// <summary>The Equals keyboard key.</summary>
    Equals = 0x0D,

    /// <summary>The Backspace keyboard key.</summary>
    Backspace = 0x0E,

    /// <summary>The Tab keyboard key.</summary>
    Tab = 0x0F,

    /// <summary>The Q keyboard key.</summary>
    Q = 0x10,

    /// <summary>The W keyboard key.</summary>
    W = 0x11,

    /// <summary>The E keyboard key.</summary>
    E = 0x12,

    /// <summary>The R keyboard key.</summary>
    R = 0x13,

    /// <summary>The T keyboard key.</summary>
    T = 0x14,

    /// <summary>The Y keyboard key.</summary>
    Y = 0x15,

    /// <summary>The U keyboard key.</summary>
    U = 0x16,

    /// <summary>The I keyboard key.</summary>
    I = 0x17,

    /// <summary>The O keyboard key.</summary>
    O = 0x18,

    /// <summary>The P keyboard key.</summary>
    P = 0x19,

    /// <summary>The Left Bracket keyboard key.</summary>
    LeftBracket = 0x1A,

    /// <summary>The Right Bracket keyboard key.</summary>
    RightBracket = 0x1B,

    /// <summary>The Enter keyboard key.</summary>
    Enter = 0x1C,

    /// <summary>The Left Control keyboard key.</summary>
    LeftControl = 0x1D,

    /// <summary>The A keyboard key.</summary>
    A = 0x1E,

    /// <summary>The S keyboard key.</summary>
    S = 0x1F,

    /// <summary>The D keyboard key.</summary>
    D = 0x20,

    /// <summary>The F keyboard key.</summary>
    F = 0x21,

    /// <summary>The G keyboard key.</summary>
    G = 0x22,

    /// <summary>The H keyboard key.</summary>
    H = 0x23,

    /// <summary>The J keyboard key.</summary>
    J = 0x24,

    /// <summary>The K keyboard key.</summary>
    K = 0x25,

    /// <summary>The L keyboard key.</summary>
    L = 0x26,

    /// <summary>The Semicolon keyboard key.</summary>
    Semicolon = 0x27,

    /// <summary>The Apostrophe keyboard key.</summary>
    Apostrophe = 0x28,

    /// <summary>The Grave keyboard key.</summary>
    Grave = 0x29,

    /// <summary>The Left Shift keyboard key.</summary>
    LeftShift = 0x2A,

    /// <summary>The Backslash keyboard key.</summary>
    Backslash = 0x2B,

    /// <summary>The Z keyboard key.</summary>
    Z = 0x2C,

    /// <summary>The X keyboard key.</summary>
    X = 0x2D,

    /// <summary>The C keyboard key.</summary>
    C = 0x2E,

    /// <summary>The V keyboard key.</summary>
    V = 0x2F,

    /// <summary>The B keyboard key.</summary>
    B = 0x30,

    /// <summary>The N keyboard key.</summary>
    N = 0x31,

    /// <summary>The M keyboard key.</summary>
    M = 0x32,

    /// <summary>The Comma keyboard key.</summary>
    Comma = 0x33,

    /// <summary>The Period keyboard key.</summary>
    Period = 0x34,

    /// <summary>The Slash keyboard key.</summary>
    Slash = 0x35,

    /// <summary>The Right Shift keyboard key.</summary>
    RightShift = 0x36,

    /// <summary>The Numpad Multiply keyboard key.</summary>
    NumpadMultiply = 0x37,

    /// <summary>The Left Alt keyboard key.</summary>
    LeftAlt = 0x38,

    /// <summary>The Space keyboard key.</summary>
    Space = 0x39,

    /// <summary>The Caps Lock keyboard key.</summary>
    CapsLock = 0x3A,

    /// <summary>The F1 keyboard key.</summary>
    F1 = 0x3B,

    /// <summary>The F2 keyboard key.</summary>
    F2 = 0x3C,

    /// <summary>The F3 keyboard key.</summary>
    F3 = 0x3D,

    /// <summary>The F4 keyboard key.</summary>
    F4 = 0x3E,

    /// <summary>The F5 keyboard key.</summary>
    F5 = 0x3F,

    /// <summary>The F6 keyboard key.</summary>
    F6 = 0x40,

    /// <summary>The F7 keyboard key.</summary>
    F7 = 0x41,

    /// <summary>The F8 keyboard key.</summary>
    F8 = 0x42,

    /// <summary>The F9 keyboard key.</summary>
    F9 = 0x43,

    /// <summary>The F10 keyboard key.</summary>
    F10 = 0x44,

    /// <summary>The Num Lock keyboard key.</summary>
    NumLock = 0x45,

    /// <summary>The Scroll Lock keyboard key.</summary>
    ScrollLock = 0x46,

    /// <summary>The Numpad7 keyboard key.</summary>
    Numpad7 = 0x47,

    /// <summary>The Numpad8 keyboard key.</summary>
    Numpad8 = 0x48,

    /// <summary>The Numpad9 keyboard key.</summary>
    Numpad9 = 0x49,

    /// <summary>The Numpad Subtract keyboard key.</summary>
    NumpadSubtract = 0x4A,

    /// <summary>The Numpad4 keyboard key.</summary>
    Numpad4 = 0x4B,

    /// <summary>The Numpad5 keyboard key.</summary>
    Numpad5 = 0x4C,

    /// <summary>The Numpad6 keyboard key.</summary>
    Numpad6 = 0x4D,

    /// <summary>The Numpad Add keyboard key.</summary>
    NumpadAdd = 0x4E,

    /// <summary>The Numpad1 keyboard key.</summary>
    Numpad1 = 0x4F,

    /// <summary>The Numpad2 keyboard key.</summary>
    Numpad2 = 0x50,

    /// <summary>The Numpad3 keyboard key.</summary>
    Numpad3 = 0x51,

    /// <summary>The Numpad0 keyboard key.</summary>
    Numpad0 = 0x52,

    /// <summary>The Numpad Decimal keyboard key.</summary>
    NumpadDecimal = 0x53,

    /// <summary>The Oem102 keyboard key.</summary>
    Oem102 = 0x56,

    /// <summary>The F11 keyboard key.</summary>
    F11 = 0x57,

    /// <summary>The F12 keyboard key.</summary>
    F12 = 0x58,

    /// <summary>The F13 keyboard key.</summary>
    F13 = 0x64,

    /// <summary>The F14 keyboard key.</summary>
    F14 = 0x65,

    /// <summary>The F15 keyboard key.</summary>
    F15 = 0x66,

    /// <summary>The Kana keyboard key.</summary>
    Kana = 0x70,

    /// <summary>The Abnt C1 keyboard key.</summary>
    AbntC1 = 0x73,

    /// <summary>The Convert keyboard key.</summary>
    Convert = 0x79,

    /// <summary>The No Convert keyboard key.</summary>
    NoConvert = 0x7B,

    /// <summary>The Yen keyboard key.</summary>
    Yen = 0x7D,

    /// <summary>The Abnt C2 keyboard key.</summary>
    AbntC2 = 0x7E,

    /// <summary>The Numpad Equals keyboard key.</summary>
    NumpadEquals = 0x8D,

    /// <summary>The Previous Track keyboard key.</summary>
    PreviousTrack = 0x90,

    /// <summary>The At keyboard key.</summary>
    At = 0x91,

    /// <summary>The Colon keyboard key.</summary>
    Colon = 0x92,

    /// <summary>The Underline keyboard key.</summary>
    Underline = 0x93,

    /// <summary>The Kanji keyboard key.</summary>
    Kanji = 0x94,

    /// <summary>The Stop keyboard key.</summary>
    Stop = 0x95,

    /// <summary>The Ax keyboard key.</summary>
    Ax = 0x96,

    /// <summary>The Unlabeled keyboard key.</summary>
    Unlabeled = 0x97,

    /// <summary>The Next Track keyboard key.</summary>
    NextTrack = 0x99,

    /// <summary>The Numpad Enter keyboard key.</summary>
    NumpadEnter = 0x9C,

    /// <summary>The Right Control keyboard key.</summary>
    RightControl = 0x9D,

    /// <summary>The Mute keyboard key.</summary>
    Mute = 0xA0,

    /// <summary>The Calculator keyboard key.</summary>
    Calculator = 0xA1,

    /// <summary>The Play Pause keyboard key.</summary>
    PlayPause = 0xA2,

    /// <summary>The Media Stop keyboard key.</summary>
    MediaStop = 0xA4,

    /// <summary>The Volume Down keyboard key.</summary>
    VolumeDown = 0xAE,

    /// <summary>The Volume Up keyboard key.</summary>
    VolumeUp = 0xB0,

    /// <summary>The Web Home keyboard key.</summary>
    WebHome = 0xB2,

    /// <summary>The Numpad Comma keyboard key.</summary>
    NumpadComma = 0xB3,

    /// <summary>The Numpad Divide keyboard key.</summary>
    NumpadDivide = 0xB5,

    /// <summary>The Print Screen keyboard key.</summary>
    PrintScreen = 0xB7,

    /// <summary>The Right Alt keyboard key.</summary>
    RightAlt = 0xB8,

    /// <summary>The Pause keyboard key.</summary>
    Pause = 0xC5,

    /// <summary>The Home keyboard key.</summary>
    Home = 0xC7,

    /// <summary>The Up keyboard key.</summary>
    Up = 0xC8,

    /// <summary>The Page Up keyboard key.</summary>
    PageUp = 0xC9,

    /// <summary>The Left keyboard key.</summary>
    Left = 0xCB,

    /// <summary>The Right keyboard key.</summary>
    Right = 0xCD,

    /// <summary>The End keyboard key.</summary>
    End = 0xCF,

    /// <summary>The Down keyboard key.</summary>
    Down = 0xD0,

    /// <summary>The Page Down keyboard key.</summary>
    PageDown = 0xD1,

    /// <summary>The Insert keyboard key.</summary>
    Insert = 0xD2,

    /// <summary>The Delete keyboard key.</summary>
    Delete = 0xD3,

    /// <summary>The Left Windows keyboard key.</summary>
    LeftWindows = 0xDB,

    /// <summary>The Right Windows keyboard key.</summary>
    RightWindows = 0xDC,

    /// <summary>The Applications keyboard key.</summary>
    Applications = 0xDD,

    /// <summary>The Power keyboard key.</summary>
    Power = 0xDE,

    /// <summary>The Sleep keyboard key.</summary>
    Sleep = 0xDF,

    /// <summary>The Wake keyboard key.</summary>
    Wake = 0xE3,

    /// <summary>The Web Search keyboard key.</summary>
    WebSearch = 0xE5,

    /// <summary>The Web Favorites keyboard key.</summary>
    WebFavorites = 0xE6,

    /// <summary>The Web Refresh keyboard key.</summary>
    WebRefresh = 0xE7,

    /// <summary>The Web Stop keyboard key.</summary>
    WebStop = 0xE8,

    /// <summary>The Web Forward keyboard key.</summary>
    WebForward = 0xE9,

    /// <summary>The Web Back keyboard key.</summary>
    WebBack = 0xEA,

    /// <summary>The My Computer keyboard key.</summary>
    MyComputer = 0xEB,

    /// <summary>The Mail keyboard key.</summary>
    Mail = 0xEC,

    /// <summary>The Media Select keyboard key.</summary>
    MediaSelect = 0xED,

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
