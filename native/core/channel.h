#pragma once

#include <cstdint>
#include <functional>
#include <map>
#include <mutex>
#include <string>
#include <string_view>
#include "serialization.h"
#include "events.h"

namespace f4forge {

// Buffers are borrowed only for the duration of the call. Receivers copy them.
// Callbacks execute synchronously and must never let exceptions cross this boundary.
using Receive = int (*)(void*, const char*, std::uint32_t) noexcept;
using Invoke = int (*)(void*, const char*, std::uint32_t, Receive, void*) noexcept;

struct ChannelBinding {
    void* context;
    Invoke call;
    std::uint64_t (*add)(void*, const char*, std::uint32_t, Invoke, void*) noexcept;
    void (*remove)(void*, std::uint64_t) noexcept;
    Invoke publish;
    std::uint64_t (*subscribe)(void*, const char*, std::uint32_t, Invoke, void*) noexcept;
    void (*unsubscribe)(void*, std::uint64_t) noexcept;
    const char* dataFormat;
};

class Channel final {
public:
    using Handler = std::function<Json(const Json&)>;
    static constexpr std::uint32_t MaxMessageBytes = 1024 * 1024;

    explicit Channel(std::shared_ptr<const IDataSerializer> serializer = CreateDataSerializer());

    std::uint64_t Add(std::string name, Handler handler);
    void Remove(std::uint64_t registration);
    std::string Call(std::string_view request);
    std::string Publish(std::string_view request);
    EventChannel& Events() noexcept;
    const IDataSerializer& Serializer() const noexcept;
    ChannelBinding Binding() noexcept;

private:
    struct Entry {
        std::uint64_t registration;
        Handler handler;
    };

    std::string Error(std::string_view code, std::string_view message) const;
    std::string Response(const DataValue& value) const;
    std::shared_ptr<const IDataSerializer> serializer_;
    std::string dataFormat_;
    // Messages and events share a lane, preventing lock inversion on nested calls.
    std::recursive_mutex dispatchMutex_;
    std::mutex mutex_;
    std::map<std::string, Entry, std::less<>> handlers_;
    std::uint64_t nextRegistration_ = 1;
    EventChannel events_;
};

}
