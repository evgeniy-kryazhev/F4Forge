#pragma once

#include "input_module.h"
#include "../framework_events.h"

namespace f4forge::core {

class BuiltinServices final {
public:
    BuiltinServices(EndpointRegistry& endpoints, EventRegistry& events) noexcept;
    ~BuiltinServices();

    bool Start() noexcept;
    void Stop() noexcept;
    void OnGameDataReady() noexcept;
    void OnGameLoaded() noexcept;
    void OnNewGame() noexcept;

    const InputModule& Input() const noexcept { return _input; }

private:
    FrameworkEvents _frameworkEvents;
    InputModule _input;
    bool _started{};
};

}
