#include "dotnet_sdk.h"

#include <Windows.h>
#define NETHOST_USE_AS_STATIC
#include <nethost.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
#include <memory>
#include <stdexcept>
#include <string>
#include <type_traits>
#include <vector>

namespace {

template<class Function>
Function Export(HMODULE library, const char* name)
{
    const auto address = GetProcAddress(library, name);
    if (address == nullptr) {
        throw std::runtime_error(std::string("Missing hostfxr export: ") + name);
    }
    return reinterpret_cast<Function>(address);
}

}

void f4forge::LoadManagedPlugins(const std::filesystem::path& pluginDirectory)
{
    const auto directory = std::filesystem::absolute(pluginDirectory);
    const auto sdk = directory / L"F4Forge";
    const auto assembly = sdk / L"F4Forge.Net.dll";
    if (!std::filesystem::exists(assembly)) {
        return;
    }

    std::vector<wchar_t> hostPath(32768);
    auto hostPathSize = hostPath.size();
    if (get_hostfxr_path(hostPath.data(), &hostPathSize, nullptr) != 0) {
        throw std::runtime_error("Install the .NET 10 x64 runtime to load F4Forge.Net.");
    }
    const std::unique_ptr<std::remove_pointer_t<HMODULE>, decltype(&FreeLibrary)> library(
        LoadLibraryW(hostPath.data()), &FreeLibrary);
    if (!library) {
        throw std::runtime_error("Cannot load hostfxr.");
    }

    const auto initialize = Export<hostfxr_initialize_for_runtime_config_fn>(library.get(), "hostfxr_initialize_for_runtime_config");
    const auto getDelegate = Export<hostfxr_get_runtime_delegate_fn>(library.get(), "hostfxr_get_runtime_delegate");
    const auto close = Export<hostfxr_close_fn>(library.get(), "hostfxr_close");
    const auto configuration = sdk / L"F4Forge.Net.runtimeconfig.json";
    hostfxr_handle context = nullptr;
    const auto result = initialize(configuration.c_str(), nullptr, &context);
    const std::unique_ptr<void, hostfxr_close_fn> contextOwner(context, close);
    if (result < 0 || context == nullptr) {
        throw std::runtime_error("Cannot initialize .NET using F4Forge.Net.runtimeconfig.json.");
    }

    void* loaderAddress = nullptr;
    if (getDelegate(context, hdt_load_assembly_and_get_function_pointer, &loaderAddress) < 0 || loaderAddress == nullptr) {
        throw std::runtime_error("Cannot acquire the .NET assembly loader.");
    }
    const auto load = reinterpret_cast<load_assembly_and_get_function_pointer_fn>(loaderAddress);
    void* startAddress = nullptr;
    if (load(assembly.c_str(), L"F4Forge.Net.Hosting.Bootstrap, F4Forge.Net", L"Start",
            UNMANAGEDCALLERSONLY_METHOD, nullptr, &startAddress) != 0 || startAddress == nullptr) {
        throw std::runtime_error("Cannot find the F4Forge.Net bootstrap method.");
    }

    using StartManaged = int(__cdecl*)(const wchar_t*);
    const auto start = reinterpret_cast<StartManaged>(startAddress);
    if (start(directory.c_str()) != 0) {
        throw std::runtime_error("Managed plugin discovery failed. See F4Forge/Net.log.");
    }
}
