#pragma once

#include "registry/endpoint_registry.h"

#include <array>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>

namespace f4forge::core {

struct ModuleState final {
    std::atomic<bool> active{ true };
    F4ForgeRuntimeHandle runtime{};
    uint32_t version{};
    std::string id;
    EndpointOwner endpointOwner;
};

class ModuleManager final {
public:
    explicit ModuleManager(EndpointRegistry& endpoints) noexcept;
    ModuleManager(const ModuleManager&) = delete;
    ModuleManager& operator=(const ModuleManager&) = delete;

    F4ForgeResult Register(
        F4ForgeRuntimeHandle runtime,
        F4ForgeStringView id,
        uint32_t version,
        F4ForgeModuleHandle* module) noexcept;

    F4ForgeResult Unregister(F4ForgeModuleHandle module) noexcept;
    ModuleState* Find(F4ForgeModuleHandle module) const noexcept;
    bool IsActive(F4ForgeModuleHandle module) const noexcept;
    EndpointOwner* Owner(F4ForgeModuleHandle module) const noexcept;

private:
    static constexpr uint32_t MaxModules = 1024;

    struct Slot final {
        std::atomic<uint32_t> generation{ 1 };
        std::unique_ptr<ModuleState> state;
    };

    ModuleState* FindUnlocked(F4ForgeModuleHandle module) const noexcept;

    EndpointRegistry& _endpoints;
    mutable std::mutex _mutex;
    std::array<Slot, MaxModules> _slots{};
    uint32_t _nextIndex = 1;
};

}
