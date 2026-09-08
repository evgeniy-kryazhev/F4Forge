#include "module_manager.h"

namespace f4forge::core {

ModuleManager::ModuleManager(EndpointRegistry& endpoints) noexcept : _endpoints(endpoints) {}

F4ForgeResult ModuleManager::Register(
    F4ForgeRuntimeHandle runtime,
    F4ForgeStringView id,
    uint32_t version,
    F4ForgeModuleHandle* module) noexcept
{
    if (module == nullptr || id.data == nullptr || id.length == 0 || version == 0)
        return F4FORGE_RESULT_INVALID_ARGUMENT;

    std::lock_guard lock(_mutex);
    if (_nextIndex >= MaxModules) return F4FORGE_RESULT_INTERNAL_ERROR;
    for (uint32_t index = 1; index < _nextIndex; ++index) {
        const auto& slot = _slots[index];
        if (!slot.state || !slot.state->active.load(std::memory_order_acquire)) continue;
        if (slot.state->id.size() == id.length &&
            std::char_traits<char>::compare(slot.state->id.data(), id.data, id.length) == 0)
            return F4FORGE_RESULT_ALREADY_REGISTERED;
    }

    const auto index = _nextIndex++;
    auto state = std::make_unique<ModuleState>();
    state->runtime = runtime;
    state->version = version;
    state->id.assign(id.data, id.length);
    const auto generation = _slots[index].generation.load(std::memory_order_relaxed);
    _slots[index].state = std::move(state);
    *module = f4forge::MakeHandle(generation, index);
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult ModuleManager::Unregister(F4ForgeModuleHandle module) noexcept
{
    std::lock_guard lock(_mutex);
    auto* state = FindUnlocked(module);
    if (state == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
    if (!state->active.exchange(false, std::memory_order_acq_rel)) return F4FORGE_RESULT_INACTIVE_MODULE;

    _endpoints.InvalidateOwner(&state->endpointOwner);
    const auto index = f4forge::HandleIndex(module);
    _slots[index].generation.fetch_add(1, std::memory_order_acq_rel);
    return F4FORGE_RESULT_SUCCESS;
}

ModuleState* ModuleManager::Find(F4ForgeModuleHandle module) const noexcept
{
    std::lock_guard lock(_mutex);
    return FindUnlocked(module);
}

bool ModuleManager::IsActive(F4ForgeModuleHandle module) const noexcept
{
    const auto* state = Find(module);
    return state != nullptr && state->active.load(std::memory_order_acquire);
}

EndpointOwner* ModuleManager::Owner(F4ForgeModuleHandle module) const noexcept
{
    auto* state = Find(module);
    return state != nullptr && state->active.load(std::memory_order_acquire)
        ? const_cast<EndpointOwner*>(&state->endpointOwner)
        : nullptr;
}

ModuleState* ModuleManager::FindUnlocked(F4ForgeModuleHandle module) const noexcept
{
    if (module == F4FORGE_INVALID_HANDLE) return nullptr;
    const auto index = f4forge::HandleIndex(module);
    if (index == 0 || index >= MaxModules) return nullptr;
    const auto& slot = _slots[index];
    if (!slot.state) return nullptr;
    if (f4forge::HandleGeneration(module) != slot.generation.load(std::memory_order_acquire)) return nullptr;
    if (!slot.state->active.load(std::memory_order_acquire)) return nullptr;
    return slot.state.get();
}

}
