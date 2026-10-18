#pragma once

#include <atomic>
#include <mutex>

namespace f4forge::console {

// F4SE plugins remain loaded for the process lifetime. Keep the trampoline alive
// too: a thread may already be forwarding to it when the host stops accepting mods.
class CommandHook final {
public:
    using Function = void (*)(const char*);

    void Install(Function target, Function replacement);
    Function Original() const noexcept { return original_.load(std::memory_order_acquire); }

private:
    std::mutex gate_;
    Function target_ = nullptr;
    Function replacement_ = nullptr;
    std::atomic<Function> original_ = nullptr;
};

}
