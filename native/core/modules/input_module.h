#pragma once

#include "../registry/event_registry.h"
#include "../registry/endpoint_registry.h"
#include "builtin_module.h"

#include "RE/B/ButtonEvent.h"
#include "RE/B/BSInputEventUser.h"
#include "RE/P/PlayerControls.h"

#include <memory>

namespace f4forge::core {

class InputModule final : public BuiltinModule {
public:
    InputModule(EndpointRegistry& endpoints, EventRegistry& events) noexcept;
    ~InputModule() override;
    InputModule(const InputModule&) = delete;
    InputModule& operator=(const InputModule&) = delete;
    bool Start() noexcept override;
    void Stop() noexcept override;

    bool InstallInputHandler() noexcept;
    bool InstallGameplayHandler() noexcept;
    bool IsMenuHandlerInstalled() const noexcept { return _menuInstalled; }
    bool IsGameplayHandlerInstalled() const noexcept { return _gameplayInstalled; }
    bool IsActive() const noexcept { return _active; }
    bool IsMainMenuActive() const noexcept;
    F4ForgeEndpointHandle KeyDownEndpoint() const noexcept { return _keyDownEndpoint; }
    static F4ForgeKey NormalizeKey(uint32_t code) noexcept;
    void Publish(const RE::ButtonEvent& event, bool isMenu) noexcept;

private:
    class MenuHandler;
    class GameplayHandler;

    bool IsGameplayInputBlocked() const noexcept;

    EndpointRegistry& _endpoints;
    EventRegistry& _events;
    EndpointOwner _owner;
    F4ForgeEndpointHandle _keyDownEndpoint{ F4FORGE_INVALID_HANDLE };
    std::unique_ptr<MenuHandler> _menuHandler;
    std::unique_ptr<GameplayHandler> _gameplayHandler;
    bool _menuInstalled{ false };
    bool _gameplayInstalled{ false };
    bool _active{ false };
};

}
