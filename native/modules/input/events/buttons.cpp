#include "buttons.h"
#include "../../game_events/vtable_hook.h"
#include "../../diagnostics/store.h"
#include <F4SE/InputMap.h>
#include <RE/Fallout.h>

namespace f4forge::input {
namespace {

using Handler = void (*)(RE::BSInputEventReceiver*, const RE::InputEvent*);
game_events::VtableHook<Handler> hook;
InputReadiness readiness;

std::vector<ButtonSnapshot> CaptureButtons(const RE::InputEvent* queue)
{
    return SnapshotButtons(queue, [](const RE::InputEvent& event) -> std::optional<ButtonSnapshot> {
        const auto* button = event.As<RE::ButtonEvent>();
        if (button == nullptr) {
            return std::nullopt;
        }
        // Match CommonLib's QJustPressed/QReleased predicates, without tracking
        // synthetic state across focus changes or device disconnections.
        const auto phase = ButtonPhase(*button);
        if (!phase.has_value()) {
            return std::nullopt;
        }
        const auto device = static_cast<std::int32_t>(button->device.get());
        const auto code = KeyCode(device, button->QIDCode(), [](std::uint32_t mask) {
            return F4SE::InputMap::GamepadMaskToKeycode(mask);
        });
        if (!code.has_value()) {
            return std::nullopt;
        }
        const auto* userEvent = button->QRawUserEvent().c_str();
        return ButtonSnapshot{device, *code, userEvent != nullptr ? userEvent : "",
            *phase ? 0.0F : button->QHeldDownSecs(), *phase};
    });
}

void OnInput(RE::BSInputEventReceiver* receiver, const RE::InputEvent* queue) noexcept
{
    ForwardButtons(game_events::Host(), [&] {
        return CaptureButtons(queue);
    }, [&] {
        hook.Original()(receiver, queue);
    });
}

REL::Relocation<std::uintptr_t> Table()
{
    // UI's first base is BSInputEventReceiver; its only virtual method is slot 0.
    return REL::Relocation<std::uintptr_t>(RE::VTABLE::UI[0]);
}

Handler ReadSlot()
{
    return reinterpret_cast<Handler>(diagnostics::CheckVtableSlot(Table().address(), 0));
}

void WriteSlot(Handler handler)
{
    auto table = Table();
    table.write_vfunc(0, handler);
    if (ReadSlot() != handler) {
        throw std::runtime_error("Cannot write the UI input receiver vtable slot.");
    }
}

const game_events::AdapterRegistration adapter({"input.buttons", [](game_events::Runtime&, const game_events::Message& message) {
    readiness.Observe(message, [] {
        if (RE::UI::GetSingleton() == nullptr) {
            return;
        }
        diagnostics::CheckAndInstall("input.buttons", "vtable", [] {
            hook.Attach(&OnInput, ReadSlot, WriteSlot);
        });
    });
}, [] {
    readiness.Reset();
    hook.Detach(&OnInput, ReadSlot, WriteSlot);
    diagnostics::Global().Hook("input.buttons", "vtable", "inactive", "Host stopped.");
}});

}
}
