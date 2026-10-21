#pragma once

#include "buttons.h"

namespace f4forge::input {

inline bool ReceivesDevice(bool camera, std::int32_t device)
{
    return camera ? device == 0 : device == 1 || device == 2;
}

// One lease spans capture, original processing and publication. If capture fails
// or the host has stopped, the original game handler still runs exactly once.
template<class Original, class Observe>
void ForwardDevice(game_events::Runtime& runtime, Original&& original, Observe&& observe) noexcept
{
    bool called = false;
    const auto forward = [&] {
        called = true;
        original();
    };
    runtime.Invoke([&] {
        observe(forward);
    });
    if (!called) {
        try {
            forward();
        } catch (const std::exception& exception) {
            runtime.Report(exception.what());
        } catch (...) {
            runtime.Report("Original input receiver failed.");
        }
    }
}

inline void PublishButtons(game_events::Runtime& runtime, const std::vector<ButtonSnapshot>& notifications)
{
    for (const auto& notification : notifications) {
        runtime.Invoke([&] {
            runtime.Publish(EventName(notification.phase), {
                {"device", notification.device},
                {"keyCode", notification.keyCode},
                {"userEvent", notification.userEvent},
                {"heldSeconds", notification.heldSeconds}});
        });
    }
}

template<class Capture, class Original>
void CaptureAndForward(game_events::Runtime& runtime, Capture&& capture, Original&& original) noexcept
{
    ForwardDevice(runtime, original, [&](auto&& forward) {
        const auto notifications = capture();
        forward();
        PublishButtons(runtime, notifications);
    });
}

}
