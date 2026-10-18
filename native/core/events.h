#pragma once

#include "serialization.h"
#include <map>
#include <mutex>
#include <vector>
#include <cstdint>
#include <atomic>

namespace f4forge {

class EventChannel final {
public:
    using Handler = std::function<void(const DataValue&)>;
    using ErrorSink = std::function<void(std::string_view)>;

    explicit EventChannel(std::recursive_mutex& dispatchMutex);
    std::uint64_t Subscribe(std::string name, Handler handler);
    void Unsubscribe(std::uint64_t registration);
    void Publish(std::string_view name, const DataValue& data);
    void SetErrorSink(ErrorSink sink);

private:
    struct Subscription {
        std::string name;
        Handler handler;
        std::atomic<bool> active = true;
    };

    std::recursive_mutex& dispatchMutex_;
    std::mutex registryMutex_;
    std::map<std::uint64_t, std::shared_ptr<Subscription>> subscriptions_;
    std::uint64_t nextRegistration_ = 1;
    ErrorSink errorSink_;
};

}
