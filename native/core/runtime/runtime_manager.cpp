#include "runtime_manager.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace f4forge::core {

F4ForgeResult RuntimeManager::RegisterProvider(const RuntimeProvider& provider)
{
    if (!IsValidProvider(provider)) return F4FORGE_RESULT_INVALID_ARGUMENT;
    std::lock_guard lock(_mutex);
    if (_providerCount >= MaxProviders) return F4FORGE_RESULT_INTERNAL_ERROR;
    for (uint32_t index = 0; index < _providerCount; ++index) {
        if (Equal(_providers[index]->info.id, provider.info.id)) return F4FORGE_RESULT_ALREADY_REGISTERED;
    }
    auto ownedProvider = std::make_unique<RuntimeProvider>(provider);
    ownedProvider->provider.info = &ownedProvider->info;
    _providers[_providerCount++] = std::move(ownedProvider);
    return F4FORGE_RESULT_SUCCESS;
}

uint32_t RuntimeManager::DiscoverDirectory(const std::filesystem::path& directory)
{
    uint32_t discovered = 0;
    try {
        if (!std::filesystem::exists(directory)) return 0;
        for (const auto& entry : std::filesystem::directory_iterator(directory)) {
            if (!entry.is_regular_file() || entry.path().extension() != L".dll") continue;
            const auto filename = entry.path().filename().wstring();
            if (filename.rfind(L"F4Forge.", 0) != 0 || entry.path().extension() != L".dll") continue;

            const auto module = LoadLibraryW(entry.path().c_str());
            if (module == nullptr) continue;
            const auto describe = reinterpret_cast<F4ForgeDescribeRuntimeFn>(
                GetProcAddress(module, "F4ForgeDescribeRuntime"));
            if (describe == nullptr) {
                FreeLibrary(module);
                continue;
            }
            const auto* providerTable = describe();
            if (providerTable == nullptr || providerTable->info == nullptr) {
                FreeLibrary(module);
                continue;
            }
            RuntimeProvider provider{ *providerTable->info, *providerTable, module };
            if (RegisterProvider(provider) != F4FORGE_RESULT_SUCCESS) {
                FreeLibrary(module);
                continue;
            }
            ++discovered;
        }
    } catch (...) {
        return discovered;
    }
    return discovered;
}

F4ForgeResult RuntimeManager::Initialize(
    F4ForgeStringView id,
    const F4ForgeHostApi* host,
    F4ForgeStringView pluginDirectory,
    F4ForgeStringView configDirectory,
    F4ForgeRuntimeHandle* runtime)
{
    if (runtime == nullptr || host == nullptr || id.data == nullptr || id.length == 0)
        return F4FORGE_RESULT_INVALID_ARGUMENT;

    RuntimeInstance* instance = nullptr;
    {
        std::lock_guard lock(_mutex);
        const RuntimeProvider* selected = nullptr;
        for (uint32_t index = 0; index < _providerCount; ++index) {
            if (Equal(_providers[index]->info.id, id)) {
                selected = _providers[index].get();
                break;
            }
        }
        if (selected == nullptr) return F4FORGE_RESULT_RUNTIME_UNAVAILABLE;
        if (_runtimeCount >= MaxRuntimes) return F4FORGE_RESULT_INTERNAL_ERROR;

        auto reserved = std::make_unique<RuntimeInstance>();
        reserved->handle = _nextRuntimeHandle++;
        reserved->provider = selected;
        reserved->state = RuntimeInstance::State::Initializing;
        instance = reserved.get();
        _runtimes[_runtimeCount++] = std::move(reserved);
    }

    F4ForgeRuntimeInitializeParams params{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeInitializeParams),
        host,
        instance->handle,
        pluginDirectory,
        configDirectory
    };
    F4ForgeResult result = F4FORGE_RESULT_INTERNAL_ERROR;
    try {
        result = instance->provider->provider.initialize(&params);
    } catch (...) {
        result = F4FORGE_RESULT_INTERNAL_ERROR;
    }

    std::lock_guard lock(_mutex);
    if (result == F4FORGE_RESULT_SUCCESS) {
        instance->state = RuntimeInstance::State::Active;
        *runtime = instance->handle;
    } else {
        instance->state = RuntimeInstance::State::Failed;
    }
    return result;
}

uint32_t RuntimeManager::InitializeAll(
    const F4ForgeHostApi* host,
    F4ForgeStringView pluginDirectory,
    F4ForgeStringView configDirectory)
{
    if (host == nullptr) return 0;
    std::array<std::string, MaxProviders> ids{};
    uint32_t count = 0;
    {
        std::lock_guard lock(_mutex);
        count = _providerCount;
        for (uint32_t index = 0; index < count; ++index)
            ids[index].assign(_providers[index]->info.id.data, _providers[index]->info.id.length);
    }

    uint32_t initialized = 0;
    for (uint32_t index = 0; index < count; ++index) {
        F4ForgeRuntimeHandle runtime = F4FORGE_INVALID_HANDLE;
        const F4ForgeStringView id{ ids[index].data(), static_cast<uint32_t>(ids[index].size()) };
        const auto result = Initialize(id, host, pluginDirectory, configDirectory, &runtime);
        if (result == F4FORGE_RESULT_SUCCESS) {
            ++initialized;
            if (host->log != nullptr) {
                const std::string message = "Runtime initialized: " + ids[index];
                host->log(2, { message.data(), static_cast<uint32_t>(message.size()) });
            }
        } else if (host->log != nullptr) {
            const std::string message = "Runtime initialization failed: " + ids[index] +
                " (result " + std::to_string(static_cast<int32_t>(result)) + ")";
            host->log(4, { message.data(), static_cast<uint32_t>(message.size()) });
        }
    }
    return initialized;
}

F4ForgeResult RuntimeManager::Shutdown(F4ForgeRuntimeHandle runtime)
{
    RuntimeInstance* instance = nullptr;
    const RuntimeProvider* provider = nullptr;
    {
        std::lock_guard lock(_mutex);
        instance = FindUnlocked(runtime);
        if (instance == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
        if (instance->state != RuntimeInstance::State::Active)
            return F4FORGE_RESULT_INACTIVE_RUNTIME;
        instance->state = RuntimeInstance::State::ShuttingDown;
        provider = instance->provider;
    }

    F4ForgeResult result = F4FORGE_RESULT_SUCCESS;
    try {
        provider->provider.shutdown(runtime);
    } catch (...) {
        result = F4FORGE_RESULT_INTERNAL_ERROR;
    }
    {
        std::lock_guard lock(_mutex);
        instance->state = RuntimeInstance::State::Inactive;
    }
    return result;
}

RuntimeInstance* RuntimeManager::Find(F4ForgeRuntimeHandle runtime) noexcept
{
    std::lock_guard lock(_mutex);
    return FindUnlocked(runtime);
}

bool RuntimeManager::HasProvider(F4ForgeStringView id) const noexcept
{
    if (id.data == nullptr) return false;
    std::lock_guard lock(_mutex);
    for (uint32_t index = 0; index < _providerCount; ++index)
        if (Equal(_providers[index]->info.id, id)) return true;
    return false;
}

uint32_t RuntimeManager::ProviderCount() const noexcept
{
    std::lock_guard lock(_mutex);
    return _providerCount;
}

bool RuntimeManager::IsValidProvider(const RuntimeProvider& provider) noexcept
{
    const auto infoHasPrefix = F4FORGE_HAS_FIELD(provider.info.structSize, F4ForgeRuntimeInfo, structSize);
    const auto infoHasVersion = F4FORGE_HAS_FIELD(provider.info.structSize, F4ForgeRuntimeInfo, providerVersion);
    const auto providerHasPrefix = F4FORGE_HAS_FIELD(provider.provider.structSize, F4ForgeRuntimeProvider, structSize);
    const auto providerHasInfo = F4FORGE_HAS_FIELD(provider.provider.structSize, F4ForgeRuntimeProvider, info);
    const auto providerHasInitialize = F4FORGE_HAS_FIELD(
        provider.provider.structSize, F4ForgeRuntimeProvider, initialize);
    const auto providerHasShutdown = F4FORGE_HAS_FIELD(provider.provider.structSize, F4ForgeRuntimeProvider, shutdown);
    const auto providerHasExecuteTask = F4FORGE_HAS_FIELD(
        provider.provider.structSize, F4ForgeRuntimeProvider, executeTask);
    return infoHasPrefix && infoHasVersion && providerHasPrefix && providerHasInfo && providerHasInitialize &&
        providerHasShutdown && providerHasExecuteTask &&
        provider.info.abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION &&
        provider.info.id.data != nullptr && provider.info.id.length != 0 &&
        provider.provider.abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION &&
        provider.provider.info != nullptr &&
        provider.provider.initialize != nullptr &&
        provider.provider.shutdown != nullptr &&
        provider.provider.executeTask != nullptr;
}

bool RuntimeManager::Equal(F4ForgeStringView left, F4ForgeStringView right) noexcept
{
    return left.length == right.length && left.data != nullptr && right.data != nullptr &&
        std::char_traits<char>::compare(left.data, right.data, left.length) == 0;
}

RuntimeInstance* RuntimeManager::FindUnlocked(F4ForgeRuntimeHandle runtime) noexcept
{
    if (runtime == F4FORGE_INVALID_HANDLE) return nullptr;
    for (uint32_t index = 0; index < _runtimeCount; ++index) {
        if (_runtimes[index] && _runtimes[index]->handle == runtime) return _runtimes[index].get();
    }
    return nullptr;
}

}
