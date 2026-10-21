# Input key events

## Source and attachment

The implementation follows real Fallout 4 receiver hooks:

- [F4SEMenuFramework InputQueueHook](https://github.com/DCCStudios/F4SEMenuFramework/blob/master/src/Hooks.cpp):
  PlayerCamera owns keyboard events and PlayerControls owns mouse/gamepad events.
- [UneducatedShooter InputEventReceiverOverride](https://github.com/clayne/UneducatedShooter/blob/main/src/main.cpp):
  processes the linked ButtonEvent queue through PerformInputProcessing and forwards
  to the previous implementation.

F4Forge hooks slot 0 of each actual `BSInputEventReceiver` vtable. Typed base casts
handle PlayerCamera's secondary base. Installation is idempotent and retries when
singletons are missing. GameLoaded, successful GameDataReady/PostLoadGame, NewGame
and InputLoaded can enable attachment; InputLoaded alone is not required.
No executable address, instruction patch, Poll hook, or fabricated event dispatcher
is used. CommonLib and its Address Library must support the installed game.

The two receivers have exclusive device ownership, even when the same queue is
passed to both. No per-button state, inferred transitions, batch counters or
additional duplicate suppression are maintained. Engine events from the selected
receiver are classified directly through CommonLib's button predicates.

## Contract

| SDK event | Wire name | Transition |
| --- | --- | --- |
| KeyDown | game.input.key.down | QJustPressed() |
| KeyPress | game.input.key.press | QHeldDown() |
| KeyUp | game.input.key.up | QReleased() |

A short tap can have no KeyPress. Analog triggers follow engine button values.
Mouse wheel, motion, sticks, text and unsupported devices are excluded.
`Device` is Keyboard=0, Mouse=1, Gamepad=2. `KeyCode` preserves keyboard codes
(Windows VK on the tested 1.11.240 runtime, W=87), adds 256 for mouse buttons,
and uses F4SE's active controller mapping for gamepad codes 266–281.
The named keyboard enum is not a promise of the same codes on older game builds.
`UserEvent` copies the engine action name when present. `HeldSeconds` is copied directly from the engine. A positive value with a zero
hold timer is Down, a positive value with a positive timer is Press, and a zero
value with a positive timer is Up. A zero value with a zero timer is ignored.
Wire payloads use the existing serializer. No interop exports are added.

```csharp
context.Events.Subscribe<KeyDown>(key => context.LogInfo($"Down {key.KeyCode}"));
context.Events.Subscribe<KeyPress>(key => context.LogInfo($"Held {key.KeyCode}"));
context.Events.Subscribe<KeyUp>(key => context.LogInfo($"Up {key.KeyCode}"));
```

Import F4Forge.Net.Events and F4Forge.Net.Input.Events. Replace ButtonPressed and
ButtonReleased subscriptions and rebuild against the matching SDK.

## Non-interference and lifetime

Payloads are copied before original processing and published afterwards. Original
receiver calls run exactly once even when capture fails or the host has stopped.
No queue link, handled flag, device state or game-control setting is written.
Callbacks run without an installation lock and exceptions are contained.
Shutdown drains active callbacks before detaching. Slots are restored only while
owned by F4Forge, preserving subsequent plugins' chains. Keep callbacks short and
do not stop the host from an input callback.

## Validation and limits

Native tests cover device ownership, code translation, copied payloads, failures,
startup retries, shutdown drainage and hook chaining. Managed tests cover wire
contracts, VK values and subscription lifetime.

In-game acceptance of this build is still required: keyboard/mouse/gamepad, taps,
holds, simultaneous keys, triggers, aiming, menus, console, Pip-Boy, focus loss and
controller disconnect. Receiver hooks observe events the game actually delivers;
they cannot guarantee releases discarded upstream, or input while those receivers
are not called. There is no synthetic input or timeout release. In particular,
menu-only/focus-loss coverage must be checked rather than inferred from unit tests.
The log distinguishes hook installation from the first captured button events.
