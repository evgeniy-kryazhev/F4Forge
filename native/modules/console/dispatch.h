#pragma once

#include "../game_events/runtime.h"
#include <optional>

namespace f4forge::console {

struct Interception {
    std::string command;
    bool skip = false;
};

template<class Before, class Route, class Original>
void ForwardCommand(game_events::Runtime& hostRuntime, const char* text, Before&& before, Route&& route, Original&& original) noexcept
{
    bool handled = false;
    std::optional<std::string> rewritten;
    hostRuntime.Invoke([&] {
        if (hostRuntime.DataReady() && text != nullptr) {
            auto decision = before(text);
            if (decision.skip) {
                handled = true;
                return;
            }
            if (decision.command != text) {
                rewritten = std::move(decision.command);
            }
            handled = route(rewritten ? rewritten->c_str() : text);
        }
    });
    if (!handled) {
        try {
            original(rewritten ? rewritten->c_str() : text);
        } catch (const std::exception& exception) {
            hostRuntime.Report(exception.what());
        } catch (...) {
            hostRuntime.Report("Unknown original console command failure.");
        }
    }
}

template<class Route, class Original>
void ForwardCommand(game_events::Runtime& hostRuntime, const char* text, Route&& route, Original&& original) noexcept
{
    ForwardCommand(hostRuntime, text, [](const char* input) {
        return Interception{input};
    }, std::forward<Route>(route), std::forward<Original>(original));
}

}
