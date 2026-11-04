#include "../extensions/transport.h"
#include "components.h"
#include <Windows.h>
#include <Xinput.h>

namespace f4forge::input_state
{
namespace
{
Channel* channel = nullptr;
std::vector<std::uint64_t> routes;

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    routes.push_back(owner.Add("input.state", [](const DataValue&) -> DataValue {
        extensions::RequireGameThread();
        const auto* ui = RE::UI::GetSingleton();
        bool paused = ui == nullptr || !game_events::Host().DataReady();
        if (ui != nullptr)
        {
            for (const auto& menu : ui->menuStack)
            {
                if (menu && menu->menuFlags.any(RE::UI_MENU_FLAGS::kPausesGame))
                {
                    paused = true;
                    break;
                }
            }
        }
        int modifiers = 0;
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
        {
            modifiers |= 1;
        }
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
        {
            modifiers |= 2;
        }
        if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
        {
            modifiers |= 4;
        }
        return {{"paused", paused}, {"modifiers", modifiers}};
    }));
    routes.push_back(owner.Add("input.controller", [](const DataValue& request) -> DataValue {
        const auto index = request.at("index").get<std::uint32_t>();
        if (index >= XUSER_MAX_COUNT)
        {
            throw std::invalid_argument("Controller index must be between zero and three.");
        }
        // Keep the system module loaded for the process lifetime: a copied API
        // pointer must never outlive the library that owns its executable code.
        static const auto library = LoadLibraryExW(L"xinput1_4.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        using GetState = DWORD(WINAPI*)(DWORD, XINPUT_STATE*);
        static const auto getState =
            library != nullptr ? reinterpret_cast<GetState>(GetProcAddress(library, "XInputGetState")) : nullptr;
        XINPUT_STATE state{};
        const bool connected = getState != nullptr && getState(index, &state) == ERROR_SUCCESS;
        return {{"connected", connected},
                {"packet", state.dwPacketNumber},
                {"buttons", state.Gamepad.wButtons},
                {"leftX", state.Gamepad.sThumbLX},
                {"leftY", state.Gamepad.sThumbLY}};
    }));
}

void Stop()
{
    if (channel == nullptr)
    {
        return;
    }
    for (const auto route : routes)
    {
        channel->Remove(route);
    }
    routes.clear();
    channel = nullptr;
}

const ComponentRegistration component({"input.state", Start, Stop});
} // namespace
} // namespace f4forge::input_state
