#pragma once

#include <atomic>
#include <mutex>
#include <stdexcept>

namespace f4forge::game_events {

// The memory access is supplied by the adapter so install/restore and hook chaining
// can be tested without writing the game's real vtables.
template<class Function>
class VtableHook final {
public:
    template<class Read, class Write>
    void Attach(Function replacement, Read&& read, Write&& write)
    {
        std::lock_guard lock(mutex_);
        if (installed_) {
            return;
        }
        const auto current = read();
        if (current == nullptr || current == replacement) {
            throw std::runtime_error("Invalid original game event handler.");
        }
        // The engine can enter the replacement immediately after write().
        original_ = current;
        write(replacement);
        installed_ = true;
    }

    template<class Read, class Write>
    void Detach(Function replacement, Read&& read, Write&& write)
    {
        std::lock_guard lock(mutex_);
        if (!installed_) {
            return;
        }
        if (read() == replacement) {
            write(original_.load());
            installed_ = false;
        }
        // Another plugin may now call our wrapper. Keep the original pointer and
        // installed flag: re-installing over that chain could create a recursion.
    }

    Function Original() const noexcept
    {
        return original_.load();
    }

private:
    std::mutex mutex_;
    std::atomic<Function> original_ = nullptr;
    bool installed_ = false;
};

}
