#pragma once

#include <mutex>
#include <utility>

namespace f4forge::core {

class LifecycleRegistry final {
public:
    void Start() noexcept
    {
        std::lock_guard lock(_mutex);
        _active = true;
    }

    void Stop() noexcept
    {
        std::lock_guard lock(_mutex);
        _active = false;
    }

    bool IsActive() const noexcept
    {
        std::lock_guard lock(_mutex);
        return _active;
    }

    template <class Callback>
    bool Invoke(Callback&& callback) noexcept
    {
        std::lock_guard lock(_mutex);
        if (!_active) return false;
        std::forward<Callback>(callback)();
        return true;
    }

private:
    mutable std::mutex _mutex;
    bool _active{};
};

}
