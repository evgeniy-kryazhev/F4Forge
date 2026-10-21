#include "buttons.h"
#include "event_delivery.h"
#include "../../diagnostics/store.h"
#include "../../game_events/vtable_hook.h"
#include <F4SE/InputMap.h>
#include <RE/Fallout.h>
#include <atomic>
#include <mutex>

namespace f4forge::input {
namespace {

// Receiver selection follows F4SEMenuFramework's InputQueueHook. Typed base
// conversion is essential: PlayerCamera's input receiver is a secondary base.
class Receiver final {
public:
    using Handler = void (*)(RE::BSInputEventReceiver*, const RE::InputEvent*);

    Receiver(const char* name, bool camera) : name_(name), camera_(camera) {}

    void Attach(RE::BSInputEventReceiver* receiver, Handler replacement)
    {
        std::lock_guard lock(installationGate_);
        if (receiver == nullptr) {
            diagnostics::Global().Hook(name_, "vtable", "waiting", "Input receiver is not available yet.");
            return;
        }
        const auto table = *reinterpret_cast<const std::uintptr_t*>(receiver);
        if (attached_) {
            if (table_ != table) {
                throw std::runtime_error("Input receiver vtable changed; restart is required.");
            }
            return;
        }
        table_ = table;
        diagnostics::CheckAndInstall(name_, "vtable", [&] {
            hook_.Attach(replacement, [&] { return Read(); }, [&](Handler handler) { Write(handler); });
        });
        replacement_ = replacement;
        attached_ = true;
        REX::INFO("Input: {} PerformInputProcessing hook installed", name_);
    }

    void Detach()
    {
        std::lock_guard lock(installationGate_);
        if (!attached_) {
            return;
        }
        hook_.Detach(replacement_, [&] { return Read(); }, [&](Handler handler) { Write(handler); });
        attached_ = false;
        observed_ = false;
        diagnostics::Global().Hook(name_, "vtable", "inactive", "Host stopped.");
    }

    void Forward(RE::BSInputEventReceiver* receiver, const RE::InputEvent* queue) noexcept
    {
        auto& runtime = game_events::Host();
        CaptureAndForward(runtime, [&] {
            // Copy payloads before the original can change or recycle events.
            return Capture(queue);
        }, [&] {
            hook_.Original()(receiver, queue);
        });
    }

private:
    std::vector<ButtonSnapshot> Capture(const RE::InputEvent* queue)
    {
        std::vector<ButtonSnapshot> notifications;
        for (auto* event = queue; event != nullptr; event = event->next) {
            const auto device = static_cast<std::int32_t>(event->device.get());
            if (!ReceivesDevice(camera_, device)) {
                continue;
            }
            const auto* button = event->As<RE::ButtonEvent>();
            if (button == nullptr) {
                continue;
            }
            const auto code = KeyCode(device, button->QIDCode(), [](std::uint32_t mask) {
                return F4SE::InputMap::GamepadMaskToKeycode(mask);
            });
            if (!code) {
                continue;
            }
            std::optional<KeyPhase> phase;
            if (button->QJustPressed()) {
                phase = KeyPhase::Down;
            } else if (button->QHeldDown()) {
                phase = KeyPhase::Press;
            } else if (button->QReleased()) {
                phase = KeyPhase::Up;
            }
            if (!phase) {
                continue;
            }
            const auto* action = button->QRawUserEvent().c_str();
            notifications.push_back({device, *code, action != nullptr ? action : "",
                button->QHeldDownSecs(), *phase});
        }
        if (!notifications.empty() && !observed_.exchange(true)) {
            REX::INFO("Input: {} received button events", name_);
        }
        return notifications;
    }

    Handler Read() const
    {
        return reinterpret_cast<Handler>(diagnostics::CheckVtableSlot(table_, 0));
    }

    void Write(Handler handler) const
    {
        REL::Relocation<std::uintptr_t> table(table_);
        table.write_vfunc(0, handler);
        if (Read() != handler) {
            throw std::runtime_error("Cannot write input receiver vtable slot.");
        }
    }

    const char* name_;
    bool camera_;
    std::mutex installationGate_;
    game_events::VtableHook<Handler> hook_;
    std::uintptr_t table_ = 0;
    Handler replacement_ = nullptr;
    bool attached_ = false;
    std::atomic<bool> observed_ = false;
};

Receiver camera("input.camera", true);
Receiver controls("input.controls", false);
InputReadiness readiness;

void OnCameraInput(RE::BSInputEventReceiver* receiver, const RE::InputEvent* queue) noexcept
{
    camera.Forward(receiver, queue);
}

void OnControlsInput(RE::BSInputEventReceiver* receiver, const RE::InputEvent* queue) noexcept
{
    controls.Forward(receiver, queue);
}

const game_events::AdapterRegistration adapter({"input.keys", [](game_events::Runtime& runtime, const game_events::Message& message) {
    readiness.Observe(message, [&] {
        runtime.Invoke([] {
            camera.Attach(static_cast<RE::BSInputEventReceiver*>(RE::PlayerCamera::GetSingleton()), &OnCameraInput);
        });
        runtime.Invoke([] {
            controls.Attach(static_cast<RE::BSInputEventReceiver*>(RE::PlayerControls::GetSingleton()), &OnControlsInput);
        });
    });
}, [] {
    readiness.Reset();
    for (auto* receiver : {&controls, &camera}) {
        try {
            receiver->Detach();
        } catch (const std::exception& exception) {
            game_events::Host().Report(exception.what());
        } catch (...) {
            game_events::Host().Report("Input receiver cleanup failed.");
        }
    }
}});

}
}
