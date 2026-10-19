#pragma once

#include "../../game_events/runtime.h"
#include <optional>
#include <vector>

namespace f4forge::input {

struct ButtonSnapshot {
    std::int32_t device;
    std::uint32_t keyCode;
    std::string userEvent;
    float heldSeconds;
    bool pressed;
};

// Gamepad conversion is injected because F4SE consults the game's controller map.
template<class GamepadConverter>
std::optional<std::uint32_t> KeyCode(std::int32_t device, std::uint32_t code, GamepadConverter&& convertGamepad)
{
    if (device == 0 && code < 256) {
        return code;
    }
    if (device == 1 && code < 8) {
        return 256 + code;
    }
    if (device == 2) {
        const auto converted = convertGamepad(code);
        if (converted >= 266 && converted < 282) {
            return converted;
        }
    }
    return std::nullopt;
}

template<class Button>
std::optional<bool> ButtonPhase(const Button& button)
{
    if (button.QJustPressed()) {
        return true;
    }
    if (button.QReleased()) {
        return false;
    }
    return std::nullopt;
}

template<class Event, class Decode>
std::vector<ButtonSnapshot> SnapshotButtons(const Event* queue, Decode&& decode)
{
    std::vector<ButtonSnapshot> notifications;
    for (const auto* event = queue; event != nullptr; event = event->next) {
        auto notification = decode(*event);
        if (notification.has_value()) {
            notifications.push_back(std::move(*notification));
        }
    }
    return notifications;
}

// Snapshot failures must not prevent the game's original receiver from running.
// Each publication has its own lease so a failed event cannot discard later buttons.
template<class Capture, class Original>
void ForwardButtons(game_events::Runtime& runtime, Capture&& capture, Original&& original) noexcept
{
    std::vector<ButtonSnapshot> notifications;
    runtime.Invoke([&] {
        notifications = capture();
    });
    try {
        original();
    } catch (const std::exception& exception) {
        runtime.Report(exception.what());
        return;
    } catch (...) {
        runtime.Report("Original input receiver failed.");
        return;
    }
    for (const auto& notification : notifications) {
        runtime.Invoke([&] {
            runtime.Publish(notification.pressed ? "game.input.button.pressed" : "game.input.button.released", {
                {"device", notification.device},
                {"keyCode", notification.keyCode},
                {"userEvent", notification.userEvent},
                {"heldSeconds", notification.heldSeconds}});
        });
    }
}

class InputReadiness final {
public:
    template<class Attach>
    void Observe(const game_events::Message& message, Attach&& attach)
    {
        if (message.Is(game_events::MessageType::InputLoaded)) {
            loaded_ = true;
        }
        if (loaded_) {
            attach();
        }
    }

    void Reset() noexcept
    {
        loaded_ = false;
    }

private:
    std::atomic<bool> loaded_ = false;
};

}
