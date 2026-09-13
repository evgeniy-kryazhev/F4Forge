#include "builtin_services.h"

namespace f4forge::core {

BuiltinServices::BuiltinServices(EndpointRegistry& endpoints, EventRegistry& events) noexcept :
    _ownedFrameworkEvents(std::make_unique<FrameworkEvents>(endpoints, events)),
    _ownedInput(std::make_unique<InputModule>(endpoints, events)),
    _ownedForms(std::make_unique<FormModule>(endpoints)),
    _frameworkEvents(_ownedFrameworkEvents.get()), _input(_ownedInput.get()), _forms(_ownedForms.get())
{}

BuiltinServices::BuiltinServices(BuiltinModule& frameworkEvents, BuiltinModule& input, FormModule& forms) noexcept :
    _frameworkEvents(&frameworkEvents), _input(&input), _forms(&forms)
{}

BuiltinServices::BuiltinServices(BuiltinModule& frameworkEvents, BuiltinModule& input) noexcept :
    _frameworkEvents(&frameworkEvents), _input(&input)
{}

BuiltinServices::~BuiltinServices()
{
    Stop();
}

bool BuiltinServices::Start() noexcept
{
    if (_started) return true;
    if (!_frameworkEvents->Start()) return false;
    if (_input->Start() && (_forms == nullptr || _forms->Start())) {
        _started = true;
        return true;
    }
    _input->Stop();
    if (_forms != nullptr) _forms->Stop();
    _frameworkEvents->Stop();
    return false;
}

void BuiltinServices::Stop() noexcept
{
    _input->Stop();
    if (_forms != nullptr) _forms->Stop();
    _frameworkEvents->Stop();
    _started = false;
}

void BuiltinServices::OnGameDataReady() noexcept
{
    if (!Start()) return;
    if (_forms != nullptr) _forms->OnGameDataReady();
    _ownedFrameworkEvents->EmitGameDataReady();
}

void BuiltinServices::OnGameLoaded() noexcept
{
    if (!Start()) return;
    _ownedFrameworkEvents->EmitGameLoaded();
}

void BuiltinServices::OnNewGame() noexcept
{
    if (!Start()) return;
    _ownedFrameworkEvents->EmitNewGame();
}

}
