#include "dotnet_sdk.h"
#include "bridge/events.h"
#include "bridge/method_registry.h"
#include <F4SE/F4SE.h>
#include <REX/REX.h>
#include <Windows.h>
#ifdef ERROR
#undef ERROR
#endif
#include <stdexcept>
#include <vector>

namespace {

void OnF4SEMessage(F4SE::MessagingInterface::Message* message)
{
    // F4SE puts the readiness flag in the pointer value itself.
    if (!message || message->type != F4SE::MessagingInterface::kGameDataReady || !message->data) {
        return;
    }
    try {
        f4forge::RegisterCommonLibEventSinks();
    } catch (const std::exception& exception) {
        REX::ERROR("F4Forge event registration: {}", exception.what());
    } catch (...) {
        REX::ERROR("F4Forge: unknown event registration failure");
    }
}

std::filesystem::path PluginDirectory()
{
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&PluginDirectory), &module)) {
        throw std::runtime_error("Cannot locate F4Forge.dll.");
    }

    std::vector<wchar_t> filename(32768);
    const auto length = GetModuleFileNameW(module, filename.data(), static_cast<DWORD>(filename.size()));
    if (length == 0 || length >= filename.size()) {
        throw std::runtime_error("Cannot resolve the F4Forge.dll directory.");
    }
    return std::filesystem::path(filename.data(), filename.data() + length).parent_path();
}

}

F4SE_PLUGIN_LOAD(const F4SE::LoadInterface* loadInterface)
{
    try {
        F4SE::Init(loadInterface);
        f4forge::GetMethodRegistry();
        const auto* messaging = F4SE::GetMessagingInterface();
        if (messaging && !messaging->RegisterListener(OnF4SEMessage)) {
            throw std::runtime_error("Cannot listen for F4SE game data readiness.");
        }
        f4forge::LoadManagedPlugins(PluginDirectory());
        return true;
    } catch (const std::exception& exception) {
        REX::ERROR("F4Forge: {}", exception.what());
    } catch (...) {
        REX::ERROR("F4Forge: unknown startup failure");
    }
    return false;
}
