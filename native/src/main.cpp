#include "components.h"
#include <F4SE/F4SE.h>
#include <REX/REX.h>
#include <Windows.h>
#ifdef ERROR
#undef ERROR
#endif
#include <vector>
#include <cstdlib>
#include <memory>

namespace {

std::unique_ptr<f4forge::Channel> channel;
f4forge::Components components;

std::filesystem::path PluginDirectory()
{
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&PluginDirectory), &module)) {
        throw std::runtime_error("Cannot locate F4Forge.Core.");
    }
    std::vector<wchar_t> filename(32768);
    const auto length = GetModuleFileNameW(module, filename.data(), static_cast<DWORD>(filename.size()));
    if (length == 0 || length >= filename.size()) {
        throw std::runtime_error("Cannot resolve F4Forge.Core directory.");
    }
    return std::filesystem::path(filename.data(), filename.data() + length).parent_path();
}

}

F4SE_PLUGIN_LOAD(const F4SE::LoadInterface* loadInterface)
{
    try {
        F4SE::Init(loadInterface);
        char* configuredFormat = nullptr;
        std::size_t formatLength = 0;
        if (_dupenv_s(&configuredFormat, &formatLength, "F4FORGE_DATA_FORMAT") != 0) {
            throw std::runtime_error("Cannot read F4FORGE_DATA_FORMAT.");
        }
        const std::unique_ptr<char, decltype(&std::free)> formatOwner(configuredFormat, &std::free);
        const std::string format = configuredFormat == nullptr ? "json" : configuredFormat;
        channel = std::make_unique<f4forge::Channel>(f4forge::CreateDataSerializer(format));
        channel->Events().SetErrorSink([](std::string_view message) {
            REX::ERROR("{}", message);
        });
        components.Start(*channel, PluginDirectory());
        REX::INFO("F4Forge.Core loaded");
        return true;
    } catch (const std::exception& exception) {
        REX::ERROR("F4Forge.Core: {}", exception.what());
        return false;
    } catch (...) {
        REX::ERROR("F4Forge.Core: unknown startup failure");
        return false;
    }
}
