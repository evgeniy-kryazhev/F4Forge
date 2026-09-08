#pragma once

#include "endpoint_registry.h"

#include <shared_mutex>
#include <string>
#include <unordered_map>

namespace f4forge::core {

class CapabilityRegistry final {
public:
    CapabilityRegistry() = default;
    CapabilityRegistry(const CapabilityRegistry&) = delete;
    CapabilityRegistry& operator=(const CapabilityRegistry&) = delete;

    F4ForgeResult Register(F4ForgeStringView id, uint32_t version, EndpointOwner* owner);
    void InvalidateOwner(EndpointOwner* owner) noexcept;
    uint32_t Query(F4ForgeStringView id, uint32_t minimumVersion) const;

private:
    struct Entry final {
        uint32_t version{};
        EndpointOwner* owner{};
    };

    mutable std::shared_mutex _mutex;
    std::unordered_map<std::string, Entry> _entries;
};

}
