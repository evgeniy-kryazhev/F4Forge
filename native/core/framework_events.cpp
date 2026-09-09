#include "framework_events.h"

#include <string>

namespace f4forge::core {

namespace {

F4ForgeResult F4FORGE_CALL FrameworkThunk(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) F4FORGE_NOEXCEPT
{
    return F4FORGE_RESULT_INVALID_ARGUMENT;
}

}

FrameworkEvents::FrameworkEvents(EndpointRegistry& endpoints, EventRegistry& events) noexcept :
    _endpoints(endpoints), _events(events)
{
    Register("framework.game_data_ready", _gameDataReady);
    Register("framework.game_loaded", _gameLoaded);
    Register("framework.new_game", _newGame);
}

void FrameworkEvents::Register(const char* name, F4ForgeEndpointHandle& endpoint) noexcept
{
    const F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_EVENT, 1, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY, 0, 0, 0,
        { name, static_cast<uint32_t>(std::char_traits<char>::length(name)) }, &FrameworkThunk, nullptr
    };
    _endpoints.Register(definition, &_owner, &endpoint);
}

void FrameworkEvents::EmitGameDataReady() const noexcept
{
    _events.Emit(_gameDataReady, nullptr, 0);
}

void FrameworkEvents::EmitGameLoaded() const noexcept
{
    _events.Emit(_gameLoaded, nullptr, 0);
}

void FrameworkEvents::EmitNewGame() const noexcept
{
    _events.Emit(_newGame, nullptr, 0);
}

}
