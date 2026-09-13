using F4Forge.DotNet.Sdk;
using F4Forge.DotNet.Runtime.Interop;

namespace F4Forge.DotNet.Runtime.Events.Input;

internal sealed class NativeKeyDownEventRouter : IDisposable
{
    private readonly NativeHostBridge? _bridge;
    private readonly Action<KeyDownEventArgs> _publish;
    private readonly object _gate = new();
    private IDisposable? _nativeSubscription;

    public NativeKeyDownEventRouter(NativeHostBridge? bridge, Action<KeyDownEventArgs> publish)
    {
        _bridge = bridge;
        _publish = publish;
        lock (_gate)
        {
            _nativeSubscription = _bridge?.SubscribeKeyDown(Dispatch);
        }
    }

    private void Dispatch(F4ForgeKeyEventData data)
    {
        if (data.IsDown == 0) return;
        lock (_gate)
        {
            _publish(new KeyDownEventArgs(
                (InputDevice)data.DeviceType,
                NormalizeKey(data.KeyCode),
                data.IsRepeat != 0,
                data.HeldSeconds,
                data.IsMenu != 0));
        }
    }

    private static Key NormalizeKey(uint code)
    {
        return (code >= 0x30 && code <= 0x39) ||
            (code >= 0x41 && code <= 0x5A) ||
            (code >= 0x70 && code <= 0x7B) ||
            (code >= 0x08 && code <= 0x09) || code == 0x0D || code == 0x13 || code == 0x14 ||
            code == 0x1B || (code >= 0x20 && code <= 0x28) || code is 0x2C or 0x2D or 0x2E or 0x5D ||
            (code >= 0x60 && code <= 0x69) || code is 0x6A or 0x6B or 0x6D or 0x6E or 0x6F ||
            code is 0x90 or 0x91 || (code >= 0xA0 && code <= 0xA5) ||
            (code >= 0xBA && code <= 0xBF) || (code >= 0xDB && code <= 0xDE)
            ? (Key)code : Key.Unknown;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _nativeSubscription?.Dispose();
            _nativeSubscription = null;
        }
    }
}
