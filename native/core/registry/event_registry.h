#pragma once

#include "endpoint_registry.h"

#include <array>
#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

namespace f4forge::core {

class EventRegistry final {
public:
    explicit EventRegistry(const EndpointRegistry& endpoints) noexcept;
    EventRegistry(const EventRegistry&) = delete;
    EventRegistry& operator=(const EventRegistry&) = delete;

    F4ForgeEventSubscriptionHandle Subscribe(
        EndpointOwner* subscriber,
        F4ForgeEndpointHandle endpoint,
        F4ForgeEventCallback callback,
        void* context);

    void Unsubscribe(F4ForgeEventSubscriptionHandle subscription) noexcept;
    void InvalidateOwner(const EndpointOwner* owner) noexcept;

    F4ForgeResult Emit(
        F4ForgeEndpointHandle endpoint,
        const void* payload,
        uint32_t payloadSize) const noexcept;

private:
    static constexpr uint32_t MaxSubscriptions = 4096;

    struct Subscription final {
        std::atomic<bool> active{ true };
        std::atomic<uint32_t> generation{ 1 };
        F4ForgeEndpointHandle endpoint{};
        F4ForgeEventCallback callback{};
        void* context{};
        EndpointOwner* publisher{};
        EndpointOwner* subscriber{};
    };

    const Subscription* GetSubscription(F4ForgeEventSubscriptionHandle subscription) const noexcept;

    const EndpointRegistry& _endpoints;
    mutable std::mutex _mutex;
    std::array<std::atomic<Subscription*>, MaxSubscriptions> _subscriptions{};
    std::array<std::unique_ptr<Subscription>, MaxSubscriptions> _ownedSubscriptions{};
    std::vector<std::unique_ptr<Subscription>> _retiredSubscriptions;
    std::vector<uint32_t> _freeIndices;
    uint32_t _nextIndex = 1;
};

}
