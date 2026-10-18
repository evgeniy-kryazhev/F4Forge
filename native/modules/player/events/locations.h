#pragma once

#include "../../game_events/runtime.h"
#include "../../game_events/references.h"

namespace f4forge::game_events {

struct LocationSnapshot {
    std::uint32_t actor;
    std::uint32_t previous;
    std::uint32_t current;
};

// Shared by the real vtable hook and tests: original behavior runs exactly once,
// its return value is preserved, and notification failures cannot change it.
template<class Original>
auto ForwardLocation(Runtime& runtime, const LocationSnapshot& event, Original&& original)
{
    const auto result = original();
    runtime.Invoke([&] {
        if (event.actor == PlayerFormId && event.previous != event.current) {
            runtime.Publish("game.player.location.changed", {
                {"previousLocationFormId", event.previous}, {"newLocationFormId", event.current}});
        }
    });
    return result;
}

}
