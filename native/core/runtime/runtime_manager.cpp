#include "runtime_manager.h"

namespace f4forge::core {

F4ForgeResult RuntimeManager::RegisterProvider(const RuntimeProvider& provider) noexcept
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

F4ForgeResult RuntimeManager::Initialize(
    F4ForgeStringView id,
    const F4ForgeHostApi* host,
    F4ForgeStringView pluginDirectory,
    F4ForgeStringView configDirectory,
    F4ForgeRuntimeHandle* runtime) noexcept
{
    if (runtime == nullptr || host == nullptr || id.data == nullptr || id.length == 0)
        return F4FORGE_RESULT_INVALID_ARGUMENT;

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

    auto instance = std::make_unique<RuntimeInstance>();
    instance->handle = _nextRuntimeHandle++;
    instance->provider = selected;
    F4ForgeRuntimeInitializeParams params{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeInitializeParams),
        host,
        instance->handle,
        pluginDirectory,
        configDirectory
    };
    const auto result = selected->provider.initialize(&params);
    if (result != F4FORGE_RESULT_SUCCESS) return result;

    instance->active = true;
    *runtime = instance->handle;
    _runtimes[_runtimeCount++] = std::move(instance);
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult RuntimeManager::Shutdown(F4ForgeRuntimeHandle runtime) noexcept
{
    std::lock_guard lock(_mutex);
    auto* instance = FindUnlocked(runtime);
    if (instance == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
    if (!instance->active) return F4FORGE_RESULT_INACTIVE_RUNTIME;
    instance->active = false;
    instance->provider->provider.shutdown(runtime);
    return F4FORGE_RESULT_SUCCESS;
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

bool RuntimeManager::IsValidProvider(const RuntimeProvider& provider) noexcept
{
    return provider.info.abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION &&
        provider.info.structSize >= sizeof(F4ForgeRuntimeInfo) &&
        provider.info.id.data != nullptr && provider.info.id.length != 0 &&
        provider.provider.abiVersion == F4FORGE_RUNTIME_PROVIDER_ABI_VERSION &&
        provider.provider.structSize >= sizeof(F4ForgeRuntimeProvider) &&
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
