#pragma once

#include "registry/event_registry.h"
#include "registry/endpoint_registry.h"

namespace f4forge::core {

class FrameworkEvents final {
public:
    FrameworkEvents(EndpointRegistry& endpoints, EventRegistry& events) noexcept;
    F4ForgeEndpointHandle GameDataReady() const noexcept { return _gameDataReady; }
    F4ForgeEndpointHandle GameLoaded() const noexcept { return _gameLoaded; }
    F4ForgeEndpointHandle NewGame() const noexcept { return _newGame; }

    void EmitGameDataReady() const noexcept;
    void EmitGameLoaded() const noexcept;
    void EmitNewGame() const noexcept;

private:
    void Register(const char* name, F4ForgeEndpointHandle& endpoint) noexcept;

    EndpointRegistry& _endpoints;
    EventRegistry& _events;
    EndpointOwner _owner;
    F4ForgeEndpointHandle _gameDataReady{ F4FORGE_INVALID_HANDLE };
    F4ForgeEndpointHandle _gameLoaded{ F4FORGE_INVALID_HANDLE };
    F4ForgeEndpointHandle _newGame{ F4FORGE_INVALID_HANDLE };
};

}
