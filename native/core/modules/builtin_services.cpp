#include "builtin_services.h"

namespace f4forge::core {

BuiltinServices::BuiltinServices(EndpointRegistry& endpoints, EventRegistry& events) noexcept :
    _frameworkEvents(endpoints, events), _input(endpoints, events)
{}

BuiltinServices::~BuiltinServices()
{
    Stop();
}

bool BuiltinServices::Start() noexcept
{
    if (_started) return true;
    if (!_frameworkEvents.Start()) return false;
    _started = _input.Start();
    return _started;
}

void BuiltinServices::Stop() noexcept
{
    if (!_started) return;
    _input.Stop();
    _frameworkEvents.Stop();
    _started = false;
}

void BuiltinServices::OnGameDataReady() noexcept
{
    _input.Start();
    _frameworkEvents.EmitGameDataReady();
}

void BuiltinServices::OnGameLoaded() noexcept
{
    _input.Start();
    _frameworkEvents.EmitGameLoaded();
}

void BuiltinServices::OnNewGame() noexcept
{
    _input.Start();
    _frameworkEvents.EmitNewGame();
}

}
