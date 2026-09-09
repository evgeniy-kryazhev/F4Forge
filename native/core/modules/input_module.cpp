#include "input_module.h"

#include "RE/C/Console.h"
#include "RE/C/ControlMap.h"
#include "RE/M/MenuControls.h"
#include "RE/M/MainMenu.h"
#include "RE/U/UI.h"

namespace f4forge::core {

namespace {

InputModule* g_inputModule{};

class MenuHandler final : public RE::BSInputEventUser {
public:
    bool ShouldHandleEvent(const RE::InputEvent*) override { return true; }

    void OnButtonEvent(const RE::ButtonEvent* event) override
    {
        if (event != nullptr && event->device.get() == RE::INPUT_DEVICE::kKeyboard && event->QPressed() &&
            g_inputModule != nullptr && g_inputModule->IsMainMenuActive())
            g_inputModule->Publish(*event, true);
    }
};

MenuHandler g_menuHandler;

F4ForgeResult F4FORGE_CALL InputEndpointThunk(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) F4FORGE_NOEXCEPT
{
    return F4FORGE_RESULT_INVALID_ARGUMENT;
}

bool IsKeyboardKey(uint32_t code) noexcept
{
    return (code >= 0x30 && code <= 0x39) ||
        (code >= 0x41 && code <= 0x5A) ||
        (code >= 0x70 && code <= 0x7B) ||
        (code >= 0x08 && code <= 0x09) || code == 0x0D || code == 0x13 || code == 0x14 || code == 0x1B ||
        (code >= 0x20 && code <= 0x28) || code == 0x2C || code == 0x2D || code == 0x2E || code == 0x5D ||
        (code >= 0x60 && code <= 0x69) || code == 0x6A || code == 0x6B || (code >= 0x6D && code <= 0x6F) ||
        code == 0x90 || code == 0x91 || (code >= 0xA0 && code <= 0xA5) ||
        (code >= 0xBA && code <= 0xBF) || (code >= 0xDB && code <= 0xDE);
}

}

class InputModule::GameplayHandler final : public RE::PlayerInputHandler {
public:
    GameplayHandler(RE::PlayerControlsData& data, InputModule& owner) noexcept :
        RE::PlayerInputHandler(data), _owner(owner)
    {}

    bool ShouldHandleEvent(const RE::InputEvent*) override { return true; }

    void OnButtonEvent(const RE::ButtonEvent* event) override
    {
        if (event != nullptr && event->device.get() == RE::INPUT_DEVICE::kKeyboard && event->QPressed() &&
            !_owner.IsGameplayInputBlocked())
            _owner.Publish(*event, false);
    }

private:
    InputModule& _owner;
};

InputModule::InputModule(EndpointRegistry& endpoints, EventRegistry& events) noexcept :
    _endpoints(endpoints), _events(events)
{
    g_inputModule = this;
    const F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_EVENT, 1, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY, 0, 0, sizeof(F4ForgeKeyEventData),
        { "input.key.down", sizeof("input.key.down") - 1 }, &InputEndpointThunk, nullptr
    };
    _endpoints.Register(definition, &_owner, &_keyDownEndpoint);
}

InputModule::~InputModule()
{
    if (g_inputModule == this) g_inputModule = nullptr;
}

bool InputModule::InstallInputHandler() noexcept
{
    if (_menuInstalled && _gameplayInstalled) return true;
    try {
        if (!_menuInstalled) {
            auto* controls = RE::MenuControls::GetSingleton();
            if (controls == nullptr) {
                REX::ERROR("F4Forge: MenuControls singleton unavailable; input handler will be retried");
                return false;
            }
            controls->RegisterHandler(&g_menuHandler);
            _menuInstalled = true;
        }
        InstallGameplayHandler();
        return _menuInstalled;
    } catch (...) {
        return false;
    }
}

bool InputModule::InstallGameplayHandler() noexcept
{
    if (_gameplayInstalled) return true;
    try {
        auto* controls = RE::PlayerControls::GetSingleton();
        if (controls == nullptr) return false;
        _gameplayHandler = std::make_unique<GameplayHandler>(controls->data, *this);
        controls->RegisterHandler(_gameplayHandler.get());
        _gameplayInstalled = true;
        return true;
    } catch (...) {
        _gameplayHandler.reset();
        return false;
    }
}

F4ForgeKey InputModule::NormalizeKey(uint32_t code) noexcept
{
    return IsKeyboardKey(code) ? static_cast<F4ForgeKey>(code) : F4FORGE_KEY_UNKNOWN;
}

bool InputModule::IsGameplayInputBlocked() const noexcept
{
    try {
        const auto* controlMap = RE::ControlMap::GetSingleton();
        if (controlMap != nullptr && controlMap->byTextEntryCount > 0) return true;
        const auto* ui = RE::UI::GetSingleton();
        return ui != nullptr && (ui->GetMenuOpen<RE::Console>() || ui->GetMenuOpen<RE::MainMenu>());
    } catch (...) {
        return true;
    }
}

bool InputModule::IsMainMenuActive() const noexcept
{
    try {
        const auto* ui = RE::UI::GetSingleton();
        return ui != nullptr && ui->GetMenuOpen<RE::MainMenu>();
    } catch (...) {
        return false;
    }
}

void InputModule::Publish(const RE::ButtonEvent& event, bool isMenu) noexcept
{
    const auto heldSeconds = event.QHeldDownSecs();
    const F4ForgeKeyEventData data{
        sizeof(F4ForgeKeyEventData), F4FORGE_INPUT_KEYBOARD,
        static_cast<uint32_t>(NormalizeKey(event.QIDCode())), 1,
        heldSeconds > 0.0F ? 1u : 0u, heldSeconds, isMenu ? 1u : 0u
    };
    _events.Emit(_keyDownEndpoint, &data, sizeof(data));
}

}
