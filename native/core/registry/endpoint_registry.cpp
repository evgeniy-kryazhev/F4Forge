#include "endpoint_registry.h"

#include <cstring>

namespace f4forge::core {

EndpointRegistry::EndpointRegistry() noexcept
{
    for (auto& slot : _slots) slot.store(nullptr, std::memory_order_relaxed);
}

F4ForgeResult EndpointRegistry::Register(
    const F4ForgeEndpointDefinition& definition,
    EndpointOwner* owner,
    F4ForgeEndpointHandle* endpoint)
{
    if (endpoint == nullptr || owner == nullptr || !owner->active.load(std::memory_order_acquire))
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    if (!IsValidDefinition(definition)) return F4FORGE_RESULT_INVALID_ARGUMENT;

    const auto key = MakeKey(definition.name, definition.version);
    std::lock_guard registrationLock(_registrationMutex);
    {
        std::unique_lock nameLock(_nameMutex);
        if (_byName.contains(key)) return F4FORGE_RESULT_ALREADY_REGISTERED;
    }
    if (_nextIndex >= MaxSlots) return F4FORGE_RESULT_INTERNAL_ERROR;

    auto slot = std::make_unique<Slot>();
    slot->kind = definition.kind;
    slot->version = definition.version;
    slot->flags = definition.flags;
    slot->threadPolicy = definition.threadPolicy;
    slot->requestSize = definition.requestSize;
    slot->responseSize = definition.responseSize;
    slot->payloadSize = definition.payloadSize;
    slot->name.assign(definition.name.data, definition.name.length);
    slot->thunk = definition.thunk;
    slot->context = definition.context;
    slot->owner = owner;

    const auto index = _nextIndex++;
    const auto handle = f4forge::MakeHandle(slot->generation.load(std::memory_order_relaxed), index);
    auto* slotPointer = slot.get();
    _ownedSlots[index] = std::move(slot);
    _slots[index].store(slotPointer, std::memory_order_release);
    {
        std::unique_lock nameLock(_nameMutex);
        _byName.emplace(key, handle);
    }
    *endpoint = handle;
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeEndpointHandle EndpointRegistry::Resolve(F4ForgeStringView name, uint32_t version) const
{
    if (name.data == nullptr && name.length != 0) return F4FORGE_INVALID_HANDLE;
    const auto key = MakeKey(name, version);
    std::shared_lock nameLock(_nameMutex);
    const auto it = _byName.find(key);
    if (it == _byName.end()) return F4FORGE_INVALID_HANDLE;

    const auto* slot = GetSlot(it->second);
    if (slot == nullptr || !slot->active.load(std::memory_order_acquire)) return F4FORGE_INVALID_HANDLE;
    return it->second;
}

uint32_t EndpointRegistry::Kind(F4ForgeEndpointHandle endpoint) const noexcept
{
    const auto* slot = GetSlot(endpoint);
    if (slot == nullptr || !slot->active.load(std::memory_order_acquire)) return 0;
    if (f4forge::HandleGeneration(endpoint) != slot->generation.load(std::memory_order_acquire)) return 0;
    return slot->kind;
}

uint32_t EndpointRegistry::PayloadSize(F4ForgeEndpointHandle endpoint) const noexcept
{
    const auto* slot = GetSlot(endpoint);
    if (slot == nullptr || !slot->active.load(std::memory_order_acquire)) return 0;
    if (f4forge::HandleGeneration(endpoint) != slot->generation.load(std::memory_order_acquire)) return 0;
    return slot->payloadSize;
}

EndpointOwner* EndpointRegistry::Owner(F4ForgeEndpointHandle endpoint) const noexcept
{
    const auto* slot = GetSlot(endpoint);
    if (slot == nullptr || !slot->active.load(std::memory_order_acquire)) return nullptr;
    if (f4forge::HandleGeneration(endpoint) != slot->generation.load(std::memory_order_acquire)) return nullptr;
    return slot->owner;
}

F4ForgeResult EndpointRegistry::Invoke(
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    const void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) const noexcept
{
    const auto* slot = GetSlot(endpoint);
    if (slot == nullptr) return F4FORGE_RESULT_INVALID_HANDLE;
    const auto generation = f4forge::HandleGeneration(endpoint);
    if (generation != slot->generation.load(std::memory_order_acquire)) return F4FORGE_RESULT_STALE_HANDLE;
    if (!slot->active.load(std::memory_order_acquire)) return F4FORGE_RESULT_INACTIVE_ENDPOINT;
    if (slot->kind != F4FORGE_ENDPOINT_METHOD) return F4FORGE_RESULT_INVALID_ARGUMENT;
    if (slot->owner == nullptr || !slot->owner->active.load(std::memory_order_acquire))
        return F4FORGE_RESULT_INACTIVE_MODULE;
    if (!IsThreadAllowed(*slot)) return F4FORGE_RESULT_WRONG_THREAD;
    if (!ValidateBuffers(*slot, request, requestSize, response, responseCapacity, responseSize))
        return requestSize != slot->requestSize
            ? F4FORGE_RESULT_INVALID_REQUEST_SIZE
            : F4FORGE_RESULT_INVALID_RESPONSE_CAPACITY;

    try {
        return slot->thunk(slot->context, request, requestSize, response, responseCapacity, responseSize);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

void EndpointRegistry::InvalidateOwner(EndpointOwner* owner)
{
    if (owner == nullptr) return;
    std::lock_guard registrationLock(_registrationMutex);
    for (uint32_t index = 1; index < _nextIndex; ++index) {
        auto* slot = _slots[index].load(std::memory_order_acquire);
        if (slot == nullptr || slot->owner != owner) continue;
        slot->active.store(false, std::memory_order_release);
        slot->generation.fetch_add(1, std::memory_order_acq_rel);
        const auto key = MakeKey({ slot->name.data(), static_cast<uint32_t>(slot->name.size()) }, slot->version);
        std::unique_lock nameLock(_nameMutex);
        _byName.erase(key);
    }
    owner->active.store(false, std::memory_order_release);
}

void EndpointRegistry::SetGameThreadCheck(GameThreadCheck check) noexcept
{
    _gameThreadCheck.store(check, std::memory_order_release);
}

std::string EndpointRegistry::MakeKey(F4ForgeStringView name, uint32_t version)
{
    std::string key(name.data == nullptr ? "" : name.data, name.length);
    key.push_back('\0');
    key.append(reinterpret_cast<const char*>(&version), sizeof(version));
    return key;
}

bool EndpointRegistry::IsValidDefinition(const F4ForgeEndpointDefinition& definition) noexcept
{
    if (definition.structSize < sizeof(F4ForgeEndpointDefinition)) return false;
    if (definition.kind < F4FORGE_ENDPOINT_METHOD || definition.kind > F4FORGE_ENDPOINT_INTERCEPTOR)
        return false;
    if (definition.version == 0 || definition.name.data == nullptr || definition.name.length == 0)
        return false;
    if (definition.thunk == nullptr) return false;
    if (definition.kind == F4FORGE_ENDPOINT_EVENT && definition.payloadSize == 0) return false;
    return definition.threadPolicy <= F4FORGE_THREAD_GAME_ONLY;
}

bool EndpointRegistry::ValidateBuffers(
    const Slot& slot,
    const void* request,
    uint32_t requestSize,
    const void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) noexcept
{
    if ((requestSize != 0 && request == nullptr) || requestSize != slot.requestSize) return false;
    if (slot.responseSize == VariablePayloadSize) {
        if (responseCapacity != 0 && response == nullptr) return false;
        return responseSize != nullptr;
    }
    if (responseCapacity < slot.responseSize) return false;
    if (slot.responseSize != 0 && response == nullptr) return false;
    if (responseSize != nullptr) *responseSize = slot.responseSize;
    return true;
}

const EndpointRegistry::Slot* EndpointRegistry::GetSlot(F4ForgeEndpointHandle endpoint) const noexcept
{
    if (endpoint == F4FORGE_INVALID_HANDLE) return nullptr;
    const auto index = f4forge::HandleIndex(endpoint);
    if (index == 0 || index >= MaxSlots) return nullptr;
    return _slots[index].load(std::memory_order_acquire);
}

bool EndpointRegistry::IsThreadAllowed(const Slot& slot) const noexcept
{
    if (slot.threadPolicy != F4FORGE_THREAD_GAME_ONLY) return true;
    const auto check = _gameThreadCheck.load(std::memory_order_acquire);
    return check == nullptr || check() != 0;
}

}
