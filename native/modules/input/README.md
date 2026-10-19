# Input button events

The input adapter observes the engine's input queue through slot 0 of
`RE::VTABLE::UI[0]` (`BSInputEventReceiver::PerformInputProcessing`). Address
Library resolves the vtable; existing diagnostic checks validate the slot before
installation. In-game verification of this receiver's queue coverage is required.

The adapter attaches after F4SE `InputLoaded`, retrying on later F4SE messages if
the UI singleton is unavailable. It does not depend on game-data readiness, so
menus, the console and Pip-Boy are included. Shutdown disables publication through
the game-event runtime and restores the original slot only if this adapter still
owns it. A later plugin's hook chain is preserved; Core must remain loaded while
such a chain references its callback.

Button data is copied before the original receiver runs, and published afterwards
in queue order. The original receiver runs exactly once even if snapshot capture
fails or the host has stopped. Input pointers and handled flags are never changed.
Presses match CommonLib `QJustPressed()`, releases match `QReleased()`; held
buttons do not repeat. No synthetic releases are generated on focus loss,
disconnect, loading or shutdown. Mouse wheel, text input, motion and sticks are
excluded. Gamepad triggers count as buttons when the engine emits button events.

## SDK

Use `F4Forge.Net.Input.Events` and the existing typed event subscriptions:

```csharp
using F4Forge.Net.Events;
using F4Forge.Net.Input.Events;

context.Events.Subscribe<ButtonPressed>(notification =>
{
    if (notification.KeyCode == KeyCode.A)
    {
        context.LogInfo("A pressed");
    }
});

context.Events.Subscribe<ButtonReleased>(notification =>
{
    context.LogInfo($"Released {notification.KeyCode} after {notification.HeldSeconds} seconds");
});
```

| Event name | SDK record | Fields |
| --- | --- | --- |
| `game.input.button.pressed` | `ButtonPressed` | `Device`, `KeyCode`, `UserEvent`, `HeldSeconds` |
| `game.input.button.released` | `ButtonReleased` | Same |

`KeyCode` uses the `KeyCode : uint` enum, for example `KeyCode.A`,
`KeyCode.F1`, `KeyCode.MouseLeft` and `KeyCode.GamepadA`. It remains numeric
on the wire. Unnamed engine codes are preserved as enum values.

`Device` is numeric: Keyboard = 0, Mouse = 1, Gamepad = 2. Keyboard codes are
DirectInput scan codes (0–255), not Windows virtual keys or characters. Mouse
codes are 256–263. Gamepad codes are 266–281, converted by
`F4SE::InputMap::GamepadMaskToKeycode`, including the active controller mapping.
Unknown devices and invalid codes are ignored. `UserEvent` is the raw engine
action name, or an empty string for an unassigned button. `HeldSeconds` is zero
on press and the engine's hold duration on release. Wire fields use camelCase
and the existing channel serializer; no new interop exports are added.

Callbacks run synchronously in the engine publisher's thread after the original
UI receiver returns. Keep handlers short and do not stop the host from a handler.
Subscriptions through `context.Events` are disposed when the mod stops. An event
only notifies: it cannot consume the button or cancel its action in the game.

## Verification

```powershell
xmake f -P native/tests/input -m releasedbg -y
xmake -P native/tests/input -j 2
xmake run -P native/tests/input F4Forge.Input.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

The standalone native tests exercise translation, copied snapshots, forwarding,
failure isolation, startup retries, shutdown drainage and hook chaining without
calling engine addresses. Managed tests exercise both wire contracts through
the native transport, subscription disposal and subscriber failures.

In a running supported Fallout 4/F4SE instance, check keyboard, mouse and gamepad
press/release, analog triggers, long holds and simultaneous buttons. Repeat in
the main menu, pause menu, console and Pip-Boy, and across save loads. Confirm
exactly one notification per engine transition, no held repeats, and unchanged
game actions. Check diagnostic hook `input.buttons` is active. Automated tests
do not validate the engine ABI or guarantee queue coverage in these contexts.

References: [F4SE input handling](https://github.com/ianpatt/f4se/blob/master/f4se/Hooks_Input.cpp)
and [F4SE input-ready message](https://github.com/ianpatt/f4se/blob/master/f4se/PluginAPI.h).
