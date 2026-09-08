#include "runtime/runtime_manager.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <array>
#include <cassert>
#include <filesystem>

namespace {

uint32_t initializeCalls = 0;
uint32_t shutdownCalls = 0;

F4ForgeResult F4FORGE_CALL Initialize(const F4ForgeRuntimeInitializeParams* params) noexcept
{
    assert(params != nullptr);
    assert(params->abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION);
    assert(params->host != nullptr);
    ++initializeCalls;
    return F4FORGE_RESULT_SUCCESS;
}

void F4FORGE_CALL Shutdown(F4ForgeRuntimeHandle) noexcept
{
    ++shutdownCalls;
}

void F4FORGE_CALL ExecuteTask(const F4ForgeRuntimeTask*) noexcept {}

}

int main()
{
    static const F4ForgeRuntimeInfo info{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeInfo),
        { "test", sizeof("test") - 1 },
        { "Test Runtime", sizeof("Test Runtime") - 1 },
        1
    };
    const F4ForgeRuntimeProvider providerTable{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeProvider),
        &info,
        &Initialize,
        &Shutdown,
        &ExecuteTask
    };
    const f4forge::core::RuntimeProvider provider{ info, providerTable };

    f4forge::core::RuntimeManager manager;

    std::array<wchar_t, 32768> modulePath{};
    const auto moduleLength = GetModuleFileNameW(nullptr, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    const auto fixtureDirectory = std::filesystem::path(modulePath.data(), modulePath.data() + moduleLength)
        .parent_path().parent_path().parent_path().parent_path() / "runtime-fixtures";
    assert(manager.DiscoverDirectory(fixtureDirectory) == 2);
    assert(manager.HasProvider({ "test-a", 6 }));
    assert(manager.HasProvider({ "test-b", 6 }));

    assert(manager.RegisterProvider(provider) == F4FORGE_RESULT_SUCCESS);
    assert(manager.ProviderCount() == 3);
    assert(manager.HasProvider({ "test", 4 }));
    assert(manager.RegisterProvider(provider) == F4FORGE_RESULT_ALREADY_REGISTERED);

    F4ForgeHostApi host{};
    host.abiVersion = F4FORGE_ABI_VERSION;
    host.structSize = sizeof(host);
    F4ForgeRuntimeHandle runtime = F4FORGE_INVALID_HANDLE;
    assert(manager.Initialize({ "test", 4 }, &host, { "plugins", 7 }, { "config", 6 }, &runtime)
        == F4FORGE_RESULT_SUCCESS);
    assert(runtime != F4FORGE_INVALID_HANDLE);
    assert(initializeCalls == 1);
    assert(manager.Shutdown(runtime) == F4FORGE_RESULT_SUCCESS);
    assert(shutdownCalls == 1);
    assert(manager.Shutdown(runtime) == F4FORGE_RESULT_INACTIVE_RUNTIME);
    assert(manager.Initialize({ "missing", 7 }, &host, {}, {}, &runtime)
        == F4FORGE_RESULT_RUNTIME_UNAVAILABLE);
    assert(manager.InitializeAll(&host, { "plugins", 7 }, { "config", 6 }) == 3);
    assert(initializeCalls == 2);
    assert(manager.DiscoverDirectory("C:/F4Forge/missing-runtime-directory") == 0);
    return 0;
}
