#include "module_manager.h"

#include "../registry/capability_registry.h"
#include "../registry/event_registry.h"
#include "../registry/interceptor_registry.h"
#include "../runtime/runtime_manager.h"
#include "../async/async_operation.h"

#include <chrono>

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
    if (_runtimes != nullptr && !_runtimes->CanRegisterModule(runtime))
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

    // Close dispatch admission first. Final slot retirement is deferred when
    // this call originates from one of the module's own callbacks.
    state->endpointOwner.BeginQuiescing([this, module] {
        std::lock_guard lock(_mutex);
        const auto index = f4forge::HandleIndex(module);
        if (index == 0 || index >= MaxModules) return;
        auto& slot = _slots[index];
        if (!slot.state || f4forge::HandleGeneration(module) !=
            slot.generation.load(std::memory_order_acquire)) return;
        if (slot.generation.load(std::memory_order_acquire) != UINT32_MAX) {
            slot.generation.fetch_add(1, std::memory_order_acq_rel);
            _freeIndices.push_back(index);
        }
        _retiredStates.push_back(std::move(slot.state));
    });
    if (_operations != nullptr) _operations->CancelRequester(module);
    _events.InvalidateOwner(&state->endpointOwner);
    _interceptors.InvalidateOwner(&state->endpointOwner);
    _capabilities.InvalidateOwner(&state->endpointOwner);
    _endpoints.InvalidateOwner(&state->endpointOwner);

    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult ModuleManager::WaitForQuiescence(
    F4ForgeModuleHandle module, uint32_t timeoutMilliseconds)
{
    ModuleState* state = nullptr;
    {
        std::lock_guard lock(_mutex);
        state = FindAnyUnlocked(module);
        if (state == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
    }
    const auto timeout = timeoutMilliseconds == F4FORGE_WAIT_INFINITE
        ? std::chrono::milliseconds::max()
        : std::chrono::milliseconds(timeoutMilliseconds);
    return state->endpointOwner.WaitForQuiescence(timeout)
        ? F4FORGE_RESULT_SUCCESS : F4FORGE_RESULT_TIMEOUT;
}

bool ModuleManager::UnregisterRuntime(F4ForgeRuntimeHandle runtime)
{
    std::vector<std::pair<F4ForgeModuleHandle, ModuleState*>> modules;
    {
        std::lock_guard lock(_mutex);
        for (uint32_t index = 1; index < _nextIndex; ++index) {
            const auto& slot = _slots[index];
            if (!slot.state || slot.state->runtime != runtime)
                continue;
            modules.emplace_back(
                f4forge::MakeHandle(slot.generation.load(std::memory_order_acquire), index),
                slot.state.get());
        }
    }
    bool quiesced = true;
    for (const auto& [module, state] : modules) {
        if (state->active.load(std::memory_order_acquire)) Unregister(module);
        if (!state->endpointOwner.WaitForQuiescence(std::chrono::milliseconds(100)))
            quiesced = false;
    }
    return quiesced;
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

ModuleState* ModuleManager::FindAnyUnlocked(F4ForgeModuleHandle module) const noexcept
{
    if (module == F4FORGE_INVALID_HANDLE) return nullptr;
    const auto index = f4forge::HandleIndex(module);
    if (index == 0 || index >= MaxModules) return nullptr;
    const auto& slot = _slots[index];
    if (!slot.state || f4forge::HandleGeneration(module) !=
        slot.generation.load(std::memory_order_acquire)) return nullptr;
    return slot.state.get();
}

}
