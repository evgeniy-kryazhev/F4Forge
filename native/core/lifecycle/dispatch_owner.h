#pragma once

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <mutex>

namespace f4forge::core {

class DispatchOwner;

class DispatchLease final {
public:
    DispatchLease() noexcept = default;
    explicit DispatchLease(DispatchOwner* owner) noexcept : _owner(owner) {}
    DispatchLease(const DispatchLease&) = delete;
    DispatchLease& operator=(const DispatchLease&) = delete;

    DispatchLease(DispatchLease&& other) noexcept : _owner(other._owner)
    {
        other._owner = nullptr;
    }

    DispatchLease& operator=(DispatchLease&& other) noexcept
    {
        if (this != &other) {
            Reset();
            _owner = other._owner;
            other._owner = nullptr;
        }
        return *this;
    }

    ~DispatchLease() { Reset(); }

    explicit operator bool() const noexcept { return _owner != nullptr; }
    void Reset() noexcept;

private:
    DispatchOwner* _owner{};
};

class DispatchOwner {
public:
    DispatchOwner() noexcept = default;
    DispatchOwner(const DispatchOwner&) = delete;
    DispatchOwner& operator=(const DispatchOwner&) = delete;

    DispatchLease TryAcquireDispatchLease() noexcept
    {
        std::lock_guard lock(_mutex);
        if (!active.load(std::memory_order_acquire)) return {};
        ++_inFlight;
        return DispatchLease(this);
    }

    void BeginQuiescing() noexcept
    {
        std::unique_lock lock(_mutex);
        active.store(false, std::memory_order_release);
        _quiescing = true;
        _condition.wait(lock, [this] { return _inFlight == 0; });
    }

    void MarkUnloaded() noexcept
    {
        std::lock_guard lock(_mutex);
        _quiescing = true;
        active.store(false, std::memory_order_release);
    }

    bool IsActive() const noexcept
    {
        return active.load(std::memory_order_acquire);
    }

    std::atomic<bool> active{ true };

private:
    friend class DispatchLease;

    void ReleaseDispatchLease() noexcept
    {
        std::lock_guard lock(_mutex);
        if (_inFlight == 0) return;
        --_inFlight;
        if (_quiescing && _inFlight == 0) _condition.notify_all();
    }

    mutable std::mutex _mutex;
    std::condition_variable _condition;
    uint32_t _inFlight{};
    bool _quiescing{};
};

inline void DispatchLease::Reset() noexcept
{
    if (_owner == nullptr) return;
    _owner->ReleaseDispatchLease();
    _owner = nullptr;
}

}
