#pragma once

#include "f4forge_abi.h"
#include "f4forge_handles.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <unordered_map>

namespace f4forge::core {

struct EndpointOwner {
    std::atomic<bool> active{ true };
};

class EndpointRegistry final {
public:
    using GameThreadCheck = int32_t (F4FORGE_CALL*)() F4FORGE_NOEXCEPT;

    EndpointRegistry() noexcept;
    EndpointRegistry(const EndpointRegistry&) = delete;
    EndpointRegistry& operator=(const EndpointRegistry&) = delete;

    F4ForgeResult Register(
        const F4ForgeEndpointDefinition& definition,
        EndpointOwner* owner,
        F4ForgeEndpointHandle* endpoint);

    F4ForgeEndpointHandle Resolve(F4ForgeStringView name, uint32_t version) const;

    uint32_t Kind(F4ForgeEndpointHandle endpoint) const noexcept;
    uint32_t PayloadSize(F4ForgeEndpointHandle endpoint) const noexcept;
    EndpointOwner* Owner(F4ForgeEndpointHandle endpoint) const noexcept;

    F4ForgeResult Invoke(
        F4ForgeEndpointHandle endpoint,
        const void* request,
        uint32_t requestSize,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) const noexcept;

    void InvalidateOwner(EndpointOwner* owner);
    void SetGameThreadCheck(GameThreadCheck check) noexcept;

private:
    static constexpr uint32_t MaxSlots = 4096;
    static constexpr uint32_t VariablePayloadSize = UINT32_MAX;

    struct Slot final {
        std::atomic<bool> active{ true };
        std::atomic<uint32_t> generation{ 1 };
        uint32_t kind{};
        uint32_t version{};
        uint32_t flags{};
        uint32_t threadPolicy{};
        uint32_t requestSize{};
        uint32_t responseSize{};
        uint32_t payloadSize{};
        std::string name;
        F4ForgeEndpointThunk thunk{};
        void* context{};
        EndpointOwner* owner{};
    };

    static std::string MakeKey(F4ForgeStringView name, uint32_t version);
    static bool IsValidDefinition(const F4ForgeEndpointDefinition& definition) noexcept;
    static bool ValidateBuffers(
        const Slot& slot,
        const void* request,
        uint32_t requestSize,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) noexcept;
    const Slot* GetSlot(F4ForgeEndpointHandle endpoint) const noexcept;
    bool IsThreadAllowed(const Slot& slot) const noexcept;

    mutable std::mutex _registrationMutex;
    mutable std::shared_mutex _nameMutex;
    std::array<std::atomic<Slot*>, MaxSlots> _slots{};
    std::array<std::unique_ptr<Slot>, MaxSlots> _ownedSlots{};
    std::unordered_map<std::string, F4ForgeEndpointHandle> _byName;
    uint32_t _nextIndex = 1;
    std::atomic<GameThreadCheck> _gameThreadCheck{ nullptr };
};

}
