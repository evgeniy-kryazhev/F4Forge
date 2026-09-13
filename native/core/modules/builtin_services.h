#pragma once

#include "input_module.h"
#include "../forms/form_module.h"
#include "../framework_events.h"
#include <memory>

namespace f4forge::core {

class BuiltinServices final {
public:
    BuiltinServices(EndpointRegistry& endpoints, EventRegistry& events) noexcept;
    BuiltinServices(BuiltinModule& frameworkEvents, BuiltinModule& input) noexcept;
    BuiltinServices(BuiltinModule& frameworkEvents, BuiltinModule& input, FormModule& forms) noexcept;
    ~BuiltinServices();

    bool Start() noexcept;
    void Stop() noexcept;
    void OnGameDataReady() noexcept;
    void OnGameLoaded() noexcept;
    void OnNewGame() noexcept;

    const InputModule& Input() const noexcept { return *_ownedInput; }
    const FormModule& Forms() const noexcept { return *_ownedForms; }

private:
    std::unique_ptr<FrameworkEvents> _ownedFrameworkEvents;
    std::unique_ptr<InputModule> _ownedInput;
    std::unique_ptr<FormModule> _ownedForms;
    BuiltinModule* _frameworkEvents;
    BuiltinModule* _input;
    FormModule* _forms{};
    bool _started{};
};

}
