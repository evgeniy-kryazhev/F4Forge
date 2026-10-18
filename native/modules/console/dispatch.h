#pragma once

#include "../game_events/runtime.h"

namespace f4forge::console {

template<class Route, class Original>
void ForwardCommand(game_events::Runtime& hostRuntime, const char* text, Route&& route, Original&& original) noexcept
{
    bool handled = false;
    hostRuntime.Invoke([&] {
        if (hostRuntime.DataReady() && text != nullptr) {
            handled = route(text);
        }
    });
    if (!handled) {
        try {
            original(text);
        } catch (const std::exception& exception) {
            hostRuntime.Report(exception.what());
        } catch (...) {
            hostRuntime.Report("Unknown original console command failure.");
        }
    }
}

}
