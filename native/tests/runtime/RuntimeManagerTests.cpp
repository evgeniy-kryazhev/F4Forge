#include "runtime/runtime_manager.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <array>
#include <cassert>
#include <filesystem>

namespace {

uint32_t initializeCalls = 0;
uint32_t shutdownCalls = 0;
f4forge::core::RuntimeManager* callbackManager = nullptr;

F4ForgeResult F4FORGE_CALL Initialize(const F4ForgeRuntimeInitializeParams* params) noexcept
{
    assert(params != nullptr);
    assert(params->abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION);
    assert(params->host != nullptr);
    assert(callbackManager != nullptr);
    assert(callbackManager->ProviderCount() >= 1);
    ++initializeCalls;
    return F4FORGE_RESULT_SUCCESS;
}

void F4FORGE_CALL Shutdown(F4ForgeRuntimeHandle) noexcept
{
    assert(callbackManager != nullptr);
    assert(callbackManager->ProviderCount() >= 1);
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

    static f4forge::core::RuntimeManager manager;
    callbackManager = &manager;

    std::array<wchar_t, 32768> modulePath{};
    const auto moduleLength = GetModuleFileNameW(nullptr, modulePath.data(), static_cast<DWORD>(modulePath.size()));
    const auto fixtureDirectory = std::filesystem::path(modulePath.data(), modulePath.data() + moduleLength)
        .parent_path().parent_path().parent_path().parent_path() / "runtime-fixtures";
    const auto discovered = manager.DiscoverDirectory(fixtureDirectory);
    assert(discovered == 2);
    assert(manager.HasProvider({ "test-a", 6 }));
    assert(manager.HasProvider({ "test-b", 6 }));

    const auto registerResult = manager.RegisterProvider(provider);
    assert(registerResult == F4FORGE_RESULT_SUCCESS);
    assert(manager.ProviderCount() == 3);
    assert(manager.HasProvider({ "test", 4 }));
    const auto duplicateRegisterResult = manager.RegisterProvider(provider);
    assert(duplicateRegisterResult == F4FORGE_RESULT_ALREADY_REGISTERED);

    F4ForgeHostApi host{};
    host.abiVersion = F4FORGE_ABI_VERSION;
    host.structSize = sizeof(host);
    F4ForgeRuntimeHandle runtime = F4FORGE_INVALID_HANDLE;
    const auto initializeResult = manager.Initialize(
        { "test", 4 }, &host, { "plugins", 7 }, { "config", 6 }, &runtime);
    assert(initializeResult == F4FORGE_RESULT_SUCCESS);
    assert(runtime != F4FORGE_INVALID_HANDLE);
    assert(initializeCalls == 1);
    const auto shutdownResult = manager.Shutdown(runtime);
    assert(shutdownResult == F4FORGE_RESULT_SUCCESS);
    assert(shutdownCalls == 1);
    const auto duplicateShutdownResult = manager.Shutdown(runtime);
    assert(duplicateShutdownResult == F4FORGE_RESULT_INACTIVE_RUNTIME);
    const auto missingInitializeResult = manager.Initialize({ "missing", 7 }, &host, {}, {}, &runtime);
    assert(missingInitializeResult == F4FORGE_RESULT_RUNTIME_UNAVAILABLE);
    const auto initializeAllResult = manager.InitializeAll(&host, { "plugins", 7 }, { "config", 6 });
    assert(initializeAllResult == 3);
    assert(initializeCalls == 2);
    assert(manager.DiscoverDirectory("C:/F4Forge/missing-runtime-directory") == 0);
    return 0;
}
