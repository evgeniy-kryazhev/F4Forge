#include "module_manager.h"

#include "../registry/capability_registry.h"
#include "../registry/event_registry.h"
#include "../registry/interceptor_registry.h"
#include "../runtime/runtime_manager.h"
#include "../async/async_operation.h"

namespace f4forge::core {

ModuleManager::ModuleManager(
    EndpointRegistry& endpoints,
    EventRegistry& events,
    InterceptorRegistry& interceptors,
    CapabilityRegistry& capabilities,
    RuntimeManager* runtimes,
    AsyncOperationRegistry* operations) noexcept
    : _endpoints(endpoints),
      _events(events),
      _interceptors(interceptors),
      _capabilities(capabilities),
      _runtimes(runtimes),
      _operations(operations)
{}

F4ForgeResult ModuleManager::Register(
    F4ForgeRuntimeHandle runtime,
    F4ForgeStringView id,
    uint32_t version,
    F4ForgeModuleHandle* module)
{
    if (module == nullptr || id.data == nullptr || id.length == 0 || version == 0)
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    if (runtime == F4FORGE_INVALID_HANDLE) return F4FORGE_RESULT_INVALID_ARGUMENT;

    std::lock_guard lock(_mutex);
    if (_runtimes != nullptr && !_runtimes->IsActive(runtime))
        return F4FORGE_RESULT_INACTIVE_RUNTIME;
    for (uint32_t index = 1; index < _nextIndex; ++index) {
        const auto& slot = _slots[index];
        if (!slot.state || !slot.state->active.load(std::memory_order_acquire)) continue;
        if (slot.state->id.size() == id.length &&
            std::char_traits<char>::compare(slot.state->id.data(), id.data, id.length) == 0)
            return F4FORGE_RESULT_ALREADY_REGISTERED;
    }

    uint32_t index = 0;
    if (!_freeIndices.empty()) {
        index = _freeIndices.back();
        _freeIndices.pop_back();
        _retiredStates.push_back(std::move(_slots[index].state));
    } else {
        if (_nextIndex >= MaxModules) return F4FORGE_RESULT_INTERNAL_ERROR;
        index = _nextIndex++;
    }
    auto state = std::make_unique<ModuleState>();
    state->runtime = runtime;
    state->version = version;
    state->id.assign(id.data, id.length);
    const auto generation = _slots[index].generation.load(std::memory_order_relaxed);
    _slots[index].state = std::move(state);
    *module = f4forge::MakeHandle(generation, index);
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult ModuleManager::Unregister(F4ForgeModuleHandle module)
{
    ModuleState* state = nullptr;
    {
        std::lock_guard lock(_mutex);
        state = FindUnlocked(module);
        if (state == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
        if (!state->active.exchange(false, std::memory_order_acq_rel))
            return F4FORGE_RESULT_INACTIVE_MODULE;
    }

    state->endpointOwner.BeginQuiescing();
    if (_operations != nullptr) _operations->CancelRequester(module);
    _events.InvalidateOwner(&state->endpointOwner);
    _interceptors.InvalidateOwner(&state->endpointOwner);
    _capabilities.InvalidateOwner(&state->endpointOwner);
    _endpoints.InvalidateOwner(&state->endpointOwner);

    {
        std::lock_guard lock(_mutex);
        const auto index = f4forge::HandleIndex(module);
        const auto generation = _slots[index].generation.load(std::memory_order_acquire);
        if (generation != UINT32_MAX) {
            _slots[index].generation.fetch_add(1, std::memory_order_acq_rel);
            _freeIndices.push_back(index);
        }
    }
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
