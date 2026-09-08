#include "capability_registry.h"

#include <mutex>

namespace f4forge::core {

F4ForgeResult CapabilityRegistry::Register(F4ForgeStringView id, uint32_t version, EndpointOwner* owner)
{
    if (id.data == nullptr || id.length == 0 || version == 0 || owner == nullptr)
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    if (!owner->active.load(std::memory_order_acquire)) return F4FORGE_RESULT_INACTIVE_MODULE;

    try {
        std::string key(id.data, id.length);
        std::unique_lock lock(_mutex);
        if (_entries.contains(key)) return F4FORGE_RESULT_ALREADY_REGISTERED;
        _entries.emplace(std::move(key), Entry{ version, owner });
        return F4FORGE_RESULT_SUCCESS;
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

void CapabilityRegistry::InvalidateOwner(const EndpointOwner* owner) noexcept
{
    if (owner == nullptr) return;
    std::unique_lock lock(_mutex);
    for (auto it = _entries.begin(); it != _entries.end();) {
        if (it->second.owner == owner) it = _entries.erase(it);
        else ++it;
    }
}

uint32_t CapabilityRegistry::Query(F4ForgeStringView id, uint32_t minimumVersion) const
{
    if (id.data == nullptr || id.length == 0) return 0;
    std::shared_lock lock(_mutex);
    const auto it = _entries.find(std::string(id.data, id.length));
    if (it == _entries.end() || it->second.owner == nullptr ||
        !it->second.owner->active.load(std::memory_order_acquire) || it->second.version < minimumVersion)
        return 0;
    return it->second.version;
}

}
