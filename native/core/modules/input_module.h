#pragma once

#include "../registry/event_registry.h"
#include "../registry/endpoint_registry.h"

#include "RE/B/ButtonEvent.h"
#include "RE/B/BSInputEventUser.h"
#include "RE/P/PlayerControls.h"

#include <memory>

namespace f4forge::core {

class InputModule final {
public:
    InputModule(EndpointRegistry& endpoints, EventRegistry& events) noexcept;
    ~InputModule();
    InputModule(const InputModule&) = delete;
    InputModule& operator=(const InputModule&) = delete;

    bool InstallInputHandler() noexcept;
    bool InstallGameplayHandler() noexcept;
    bool IsMenuHandlerInstalled() const noexcept { return _menuInstalled; }
    bool IsGameplayHandlerInstalled() const noexcept { return _gameplayInstalled; }
    bool IsMainMenuActive() const noexcept;
    F4ForgeEndpointHandle KeyDownEndpoint() const noexcept { return _keyDownEndpoint; }
    static F4ForgeKey NormalizeKey(uint32_t code) noexcept;
    void Publish(const RE::ButtonEvent& event, bool isMenu) noexcept;

private:
    class GameplayHandler;

    bool IsGameplayInputBlocked() const noexcept;

    EndpointRegistry& _endpoints;
    EventRegistry& _events;
    EndpointOwner _owner;
    F4ForgeEndpointHandle _keyDownEndpoint{ F4FORGE_INVALID_HANDLE };
    std::unique_ptr<GameplayHandler> _gameplayHandler;
    bool _menuInstalled{ false };
    bool _gameplayInstalled{ false };
};

}
