#pragma once

#include "../../game_events/runtime.h"
#include <optional>
#include <vector>

namespace f4forge::input {

enum class KeyPhase {
    Down,
    Press,
    Up
};

inline const char* EventName(KeyPhase phase)
{
    switch (phase) {
        case KeyPhase::Down:
            return "game.input.key.down";
        case KeyPhase::Press:
            return "game.input.key.press";
        case KeyPhase::Up:
            return "game.input.key.up";
    }
    throw std::invalid_argument("Unknown input key phase.");
}

struct ButtonSnapshot {
    std::int32_t device;
    std::uint32_t keyCode;
    std::string userEvent;
    float heldSeconds;
    KeyPhase phase;
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

class InputReadiness final {
public:
    template<class Attach>
    void Observe(const game_events::Message& message, Attach&& attach)
    {
        // F4SE declares InputLoaded, but its game-data hooks dispatch GameLoaded
        // and GameDataReady. Do not make an optional message a permanent gate.
        const bool gameDataReady = message.Is(game_events::MessageType::GameDataReady)
            && message.data != nullptr;
        const bool saveLoaded = message.Is(game_events::MessageType::PostLoadGame)
            && message.data != nullptr;
        if (message.Is(game_events::MessageType::InputLoaded)
            || message.Is(game_events::MessageType::GameLoaded)
            || message.Is(game_events::MessageType::NewGame)
            || gameDataReady || saveLoaded) {
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
