#pragma once

#include "registry/endpoint_registry.h"

#include <array>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

namespace f4forge::core {

class EventRegistry;
class InterceptorRegistry;
class CapabilityRegistry;
class RuntimeManager;
class AsyncOperationRegistry;

struct ModuleState final {
    std::atomic<bool> active{ true };
    F4ForgeRuntimeHandle runtime{};
    uint32_t version{};
    std::string id;
    EndpointOwner endpointOwner;
};

class ModuleManager final {
public:
    ModuleManager(
        EndpointRegistry& endpoints,
        EventRegistry& events,
        InterceptorRegistry& interceptors,
        CapabilityRegistry& capabilities,
        RuntimeManager* runtimes = nullptr,
        AsyncOperationRegistry* operations = nullptr) noexcept;
    ModuleManager(const ModuleManager&) = delete;
    ModuleManager& operator=(const ModuleManager&) = delete;

    F4ForgeResult Register(
        F4ForgeRuntimeHandle runtime,
        F4ForgeStringView id,
        uint32_t version,
        F4ForgeModuleHandle* module);

    F4ForgeResult Unregister(F4ForgeModuleHandle module);
    bool UnregisterRuntime(F4ForgeRuntimeHandle runtime);
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
    EventRegistry& _events;
    InterceptorRegistry& _interceptors;
    CapabilityRegistry& _capabilities;
    RuntimeManager* _runtimes{};
    AsyncOperationRegistry* _operations{};
    mutable std::mutex _mutex;
    std::array<Slot, MaxModules> _slots{};
    std::vector<std::unique_ptr<ModuleState>> _retiredStates;
    std::vector<uint32_t> _freeIndices;
    uint32_t _nextIndex = 1;
};

}
