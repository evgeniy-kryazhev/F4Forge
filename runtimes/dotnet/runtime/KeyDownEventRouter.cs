using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime;

internal sealed class KeyDownEventRouter : IDisposable
{
    private readonly NativeHostBridge? _bridge;
    private readonly List<Registration> _registrations = [];
    private readonly object _gate = new();
    private readonly KeyDownEventArgs _args = new(InputDevice.Keyboard, Key.Unknown, false, 0, false);
    private IDisposable? _nativeSubscription;

    public KeyDownEventRouter(NativeHostBridge? bridge)
    {
        _bridge = bridge;
    }

    public IDisposable? Subscribe(PluginLoader.PluginInstance owner, KeyDownHandler handler)
    {
        lock (_gate)
        {
            _nativeSubscription ??= _bridge?.SubscribeKeyDown(Dispatch);
            if (_bridge != null && _nativeSubscription == null) return null;
            var registration = new Registration(this, owner, handler);
            _registrations.Add(registration);
            return registration;
        }
    }

    public void Unsubscribe(KeyDownHandler handler)
    {
        lock (_gate)
        {
            for (var index = _registrations.Count - 1; index >= 0; --index)
            {
                if (_registrations[index].Handler == handler)
                {
                    _registrations[index].Active = false;
                    _registrations.RemoveAt(index);
                    break;
                }
            }
        }
    }

    private void Remove(Registration registration)
    {
        lock (_gate)
        {
            registration.Active = false;
            _registrations.Remove(registration);
        }
    }

    private void Dispatch(F4ForgeKeyEventData data)
    {
        if (data.IsDown == 0) return;
        lock (_gate)
        {
            _args.Device = (InputDevice)data.DeviceType;
            _args.Key = NormalizeKey(data.KeyCode);
            _args.IsRepeat = data.IsRepeat != 0;
            _args.HeldSeconds = data.HeldSeconds;
            _args.IsMenu = data.IsMenu != 0;
            for (var index = 0; index < _registrations.Count; ++index)
            {
                var registration = _registrations[index];
                if (!registration.Active || !registration.Owner.TryAcquireDispatchLease(out var lease)) continue;
                using (lease)
                {
                    try { registration.Handler(_args); }
                    catch (Exception exception)
                    {
                        Logger.Error($"Managed key handler failed: plugin={registration.Owner.Id}: {exception}");
                    }
                }
            }
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
            foreach (var registration in _registrations) registration.Active = false;
            _registrations.Clear();
            _nativeSubscription?.Dispose();
            _nativeSubscription = null;
        }
    }

    private sealed class Registration(KeyDownEventRouter router, PluginLoader.PluginInstance owner,
        KeyDownHandler handler) : IDisposable
    {
        public PluginLoader.PluginInstance Owner { get; } = owner;
        public KeyDownHandler Handler { get; } = handler;
        public bool Active { get; set; } = true;
        public void Dispose() => router.Remove(this);
    }
}
