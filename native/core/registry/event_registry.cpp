#include "event_registry.h"

namespace f4forge::core {

EventRegistry::EventRegistry(const EndpointRegistry& endpoints) noexcept : _endpoints(endpoints)
{
    for (auto& subscription : _subscriptions) subscription.store(nullptr, std::memory_order_relaxed);
}

F4ForgeEventSubscriptionHandle EventRegistry::Subscribe(
    EndpointOwner* subscriber,
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context)
{
    if (subscriber == nullptr || callback == nullptr || _endpoints.Kind(endpoint) != F4FORGE_ENDPOINT_EVENT)
        return F4FORGE_INVALID_HANDLE;
    // cppcheck-suppress constVariablePointer
    // cppcheck-suppress constVariablePointer
    auto* publisher = _endpoints.Owner(endpoint);
    if (publisher == nullptr)
        return F4FORGE_INVALID_HANDLE;

    std::lock_guard lock(_mutex);
    auto publisherLease = publisher->TryAcquireDispatchLease();
    auto subscriberLease = subscriber->TryAcquireDispatchLease();
    if (!publisherLease || !subscriberLease) return F4FORGE_INVALID_HANDLE;
    uint32_t index = 0;
    uint32_t generation = 1;
    if (!_freeIndices.empty()) {
        index = _freeIndices.back();
        _freeIndices.pop_back();
        generation = _ownedSubscriptions[index]->generation.load(std::memory_order_acquire);
        _retiredSubscriptions.push_back(std::move(_ownedSubscriptions[index]));
    } else {
        if (_nextIndex >= MaxSubscriptions) return F4FORGE_INVALID_HANDLE;
        index = _nextIndex++;
    }
    auto subscription = std::make_unique<Subscription>();
    subscription->generation.store(generation, std::memory_order_relaxed);
    subscription->endpoint = endpoint;
    subscription->callback = callback;
    subscription->context = context;
    subscription->publisher = publisher;
    subscription->subscriber = subscriber;
    const auto handle = f4forge::MakeHandle(subscription->generation.load(std::memory_order_relaxed), index);
    auto* pointer = subscription.get();
    _ownedSubscriptions[index] = std::move(subscription);
    _subscriptions[index].store(pointer, std::memory_order_release);
    return handle;
}

void EventRegistry::Unsubscribe(F4ForgeEventSubscriptionHandle subscription) noexcept
{
    std::lock_guard lock(_mutex);
    const auto index = f4forge::HandleIndex(subscription);
    if (index == 0 || index >= MaxSubscriptions) return;
    auto* current = _subscriptions[index].load(std::memory_order_acquire);
    if (current == nullptr || !current->active.load(std::memory_order_acquire) ||
        f4forge::HandleGeneration(subscription) != current->generation.load(std::memory_order_acquire)) return;
    current->active.store(false, std::memory_order_release);
    const auto generation = current->generation.load(std::memory_order_acquire);
    if (generation != UINT32_MAX) {
        current->generation.fetch_add(1, std::memory_order_acq_rel);
        _freeIndices.push_back(index);
    }
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
    if (!_endpoints.IsThreadAllowed(endpoint)) return F4FORGE_RESULT_WRONG_THREAD;
    auto publisherLease = _endpoints.TryAcquireDispatchLease(endpoint);
    if (!publisherLease) return F4FORGE_RESULT_INACTIVE_MODULE;

    // cppcheck-suppress constVariablePointer
    auto* publisher = _endpoints.Owner(endpoint);
    if (publisher == nullptr || !publisher->IsActive())
        return F4FORGE_RESULT_INACTIVE_MODULE;

    uint32_t nextIndex;
    {
        std::lock_guard lock(_mutex);
        nextIndex = _nextIndex;
    }
    for (uint32_t index = 1; index < nextIndex; ++index) {
        const auto* subscription = _subscriptions[index].load(std::memory_order_acquire);
        if (subscription == nullptr || !subscription->active.load(std::memory_order_acquire)) continue;
        if (subscription->endpoint != endpoint || subscription->publisher != publisher) continue;
        auto subscriberLease = subscription->subscriber == nullptr
            ? DispatchLease{} : subscription->subscriber->TryAcquireDispatchLease();
        if (!subscriberLease) continue;
        if (!subscription->active.load(std::memory_order_acquire)) continue;
        try {
            subscription->callback(
                f4forge::MakeHandle(subscription->generation.load(std::memory_order_acquire), index),
                endpoint,
                payload,
                payloadSize,
                subscription->context);
        } catch (...) {
            return F4FORGE_RESULT_INTERNAL_ERROR;
        }
    }
    return F4FORGE_RESULT_SUCCESS;
}

void EventRegistry::InvalidateOwner(const EndpointOwner* owner) noexcept
{
    if (owner == nullptr) return;
    std::lock_guard lock(_mutex);
    for (uint32_t index = 1; index < _nextIndex; ++index) {
        auto* subscription = _subscriptions[index].load(std::memory_order_acquire);
        if (subscription == nullptr) continue;
        if (subscription->publisher != owner && subscription->subscriber != owner) continue;
        if (!subscription->active.exchange(false, std::memory_order_acq_rel)) continue;
        const auto generation = subscription->generation.load(std::memory_order_acquire);
        if (generation != UINT32_MAX) {
            subscription->generation.fetch_add(1, std::memory_order_acq_rel);
            _freeIndices.push_back(index);
        }
    }
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
