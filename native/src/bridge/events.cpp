#include "events.h"

#include <atomic>

namespace {

using EventCallback = std::int32_t (__cdecl*)(const char*, const void*, const f4forge::F4SEValue*) noexcept;

// The shared SDK owns one process-lifetime callback and routes names to individual plugins.
std::atomic<EventCallback> managedCallback{nullptr};

}

extern "C" __declspec(dllexport) void __cdecl SubscribeEvent(EventCallback callback) noexcept
{
    managedCallback.store(callback);
}

bool f4forge::PublishEvent(const char* name, const void* sender, const F4SEValue& args) noexcept
{
    const auto callback = managedCallback.load();
    if (callback == nullptr || name == nullptr) {
        return false;
    }
    return callback(name, sender, &args) != 0;
}
