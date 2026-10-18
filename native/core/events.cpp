#include "events.h"
#include <iostream>
#include <stdexcept>

namespace f4forge {

EventChannel::EventChannel(std::recursive_mutex& dispatchMutex) : dispatchMutex_(dispatchMutex)
{
    errorSink_ = [](std::string_view message) {
        std::cerr << message << '\n';
    };
}

std::uint64_t EventChannel::Subscribe(std::string name, Handler handler)
{
    if (name.empty() || name.size() > 256 || !handler) {
        throw std::invalid_argument("An event name and handler are required.");
    }
    std::lock_guard lock(registryMutex_);
    const auto registration = nextRegistration_++;
    subscriptions_.emplace(registration, std::make_shared<Subscription>(std::move(name), std::move(handler)));
    return registration;
}

void EventChannel::Unsubscribe(std::uint64_t registration)
{
    std::shared_ptr<Subscription> removed;
    {
        std::lock_guard registryLock(registryMutex_);
        const auto found = subscriptions_.find(registration);
        if (found != subscriptions_.end()) {
            found->second->active.store(false);
            removed = std::move(found->second);
            subscriptions_.erase(found);
        }
    }
    // Disable future delivery before waiting, without holding the registry lock.
    // Even repeated removal must wait for an earlier in-flight invocation.
    std::lock_guard dispatchLock(dispatchMutex_);
    // Captures are destroyed after both locks are released; cleanup may reenter.
}

void EventChannel::SetErrorSink(ErrorSink sink)
{
    std::lock_guard lock(dispatchMutex_);
    errorSink_ = std::move(sink);
}

void EventChannel::Publish(std::string_view name, const DataValue& data)
{
    if (name.empty() || name.size() > 256) {
        throw std::invalid_argument("An event name is required.");
    }
    std::lock_guard dispatchLock(dispatchMutex_);
    std::vector<std::shared_ptr<Subscription>> snapshot;
    {
        std::lock_guard registryLock(registryMutex_);
        for (const auto& [identifier, subscription] : subscriptions_) {
            if (subscription->name == name) {
                snapshot.push_back(subscription);
            }
        }
    }
    // New subscriptions start with the next publication. Removed entries in this
    // snapshot are skipped, even when removed by an earlier callback.
    const auto reportError = errorSink_;
    for (const auto& subscription : snapshot) {
        if (!subscription->active.load()) {
            continue;
        }
        std::string failure;
        bool failed = false;
        try {
            subscription->handler(data);
        } catch (const std::exception& exception) {
            failed = true;
            failure = exception.what();
        } catch (...) {
            failed = true;
            failure = "Unknown subscriber failure.";
        }
        if (failed && reportError) {
            try {
                reportError("Event '" + std::string(name) + "': " + failure);
            } catch (...) {
                // A failing logger must not prevent delivery to other subscribers.
            }
        }
    }
}

}
