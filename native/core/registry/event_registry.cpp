#include "event_registry.h"

namespace f4forge::core {

EventRegistry::EventRegistry(const EndpointRegistry& endpoints) noexcept : _endpoints(endpoints)
{
    for (auto& subscription : _subscriptions) subscription.store(nullptr, std::memory_order_relaxed);
}

F4ForgeEventSubscriptionHandle EventRegistry::Subscribe(
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context)
{
    if (callback == nullptr || _endpoints.Kind(endpoint) != F4FORGE_ENDPOINT_EVENT)
        return F4FORGE_INVALID_HANDLE;
    auto* owner = _endpoints.Owner(endpoint);
    if (owner == nullptr || !owner->active.load(std::memory_order_acquire)) return F4FORGE_INVALID_HANDLE;

    std::lock_guard lock(_mutex);
    if (_nextIndex >= MaxSubscriptions) return F4FORGE_INVALID_HANDLE;
    auto subscription = std::make_unique<Subscription>();
    subscription->endpoint = endpoint;
    subscription->callback = callback;
    subscription->context = context;
    subscription->owner = owner;
    const auto index = _nextIndex++;
    const auto handle = f4forge::MakeHandle(subscription->generation.load(std::memory_order_relaxed), index);
    auto* pointer = subscription.get();
    _ownedSubscriptions[index] = std::move(subscription);
    _subscriptions[index].store(pointer, std::memory_order_release);
    return handle;
}

void EventRegistry::Unsubscribe(F4ForgeEventSubscriptionHandle subscription) noexcept
{
    const auto* current = GetSubscription(subscription);
    if (current == nullptr) return;
    auto* mutableSubscription = const_cast<Subscription*>(current);
    mutableSubscription->active.store(false, std::memory_order_release);
    mutableSubscription->generation.fetch_add(1, std::memory_order_acq_rel);
}

F4ForgeResult EventRegistry::Emit(
    F4ForgeEndpointHandle endpoint,
    const void* payload,
    uint32_t payloadSize) const noexcept
{
    if (_endpoints.Kind(endpoint) != F4FORGE_ENDPOINT_EVENT) return F4FORGE_RESULT_INVALID_HANDLE;
    const auto expectedSize = _endpoints.PayloadSize(endpoint);
    if (payloadSize != expectedSize || (payloadSize != 0 && payload == nullptr))
        return F4FORGE_RESULT_INVALID_REQUEST_SIZE;
    auto* owner = _endpoints.Owner(endpoint);
    if (owner == nullptr || !owner->active.load(std::memory_order_acquire))
        return F4FORGE_RESULT_INACTIVE_MODULE;

    for (uint32_t index = 1; index < _nextIndex; ++index) {
        const auto* subscription = _subscriptions[index].load(std::memory_order_acquire);
        if (subscription == nullptr || !subscription->active.load(std::memory_order_acquire)) continue;
        if (subscription->endpoint != endpoint || subscription->owner != owner) continue;
        subscription->callback(
            f4forge::MakeHandle(subscription->generation.load(std::memory_order_acquire), index),
            endpoint,
            payload,
            payloadSize,
            subscription->context);
    }
    return F4FORGE_RESULT_SUCCESS;
}

const EventRegistry::Subscription* EventRegistry::GetSubscription(
    F4ForgeEventSubscriptionHandle subscription) const noexcept
{
    if (subscription == F4FORGE_INVALID_HANDLE) return nullptr;
    const auto index = f4forge::HandleIndex(subscription);
    if (index == 0 || index >= MaxSubscriptions) return nullptr;
    const auto* current = _subscriptions[index].load(std::memory_order_acquire);
    if (current == nullptr || !current->active.load(std::memory_order_acquire)) return nullptr;
    if (f4forge::HandleGeneration(subscription) != current->generation.load(std::memory_order_acquire)) return nullptr;
    return current;
}

}
