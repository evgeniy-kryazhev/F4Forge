#pragma once

#include <cstdint>
#include <functional>
#include <map>
#include <mutex>
#include <string>
#include <string_view>
#include <nlohmann/json.hpp>

namespace f4forge {

using Json = nlohmann::json;

// Buffers are borrowed only for the duration of the call. Receivers copy them.
// Callbacks execute synchronously and must never let exceptions cross this boundary.
using Receive = int (*)(void*, const char*, std::uint32_t) noexcept;
using Invoke = int (*)(void*, const char*, std::uint32_t, Receive, void*) noexcept;

struct ChannelBinding {
    void* context;
    Invoke call;
    std::uint64_t (*add)(void*, const char*, std::uint32_t, Invoke, void*) noexcept;
    void (*remove)(void*, std::uint64_t) noexcept;
};

class Channel final {
public:
    using Handler = std::function<Json(const Json&)>;
    static constexpr std::uint32_t MaxMessageBytes = 1024 * 1024;

    std::uint64_t Add(std::string name, Handler handler);
    void Remove(std::uint64_t registration);
    std::string Call(std::string_view request);
    ChannelBinding Binding() noexcept;

private:
    struct Entry {
        std::uint64_t registration;
        Handler handler;
    };

    // One synchronous lane permits same-thread nested calls. Removing a handler
    // waits for in-flight calls, so SDK callback contexts can then be released.
    std::recursive_mutex mutex_;
    std::map<std::string, Entry, std::less<>> handlers_;
    std::uint64_t nextRegistration_ = 1;
};

}
