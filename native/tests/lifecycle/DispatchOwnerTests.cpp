#include "lifecycle/dispatch_owner.h"

#include <atomic>
#include <cassert>
#include <chrono>
#include <thread>

int main()
{
    f4forge::core::DispatchOwner owner;
    std::atomic<bool> entered = false;
    std::atomic<bool> release = false;
    auto lease = owner.TryAcquireDispatchLease();
    assert(lease);

    std::thread callback([&] {
        entered.store(true, std::memory_order_release);
        while (!release.load(std::memory_order_acquire)) std::this_thread::yield();
    });
    while (!entered.load(std::memory_order_acquire)) std::this_thread::yield();

    std::atomic<uint32_t> teardownCalls = 0;
    owner.BeginQuiescing([&] { ++teardownCalls; });
    assert(!owner.TryAcquireDispatchLease());
    assert(!owner.WaitForQuiescence(std::chrono::milliseconds(10)));
    assert(owner.IsQuarantined());

    release.store(true, std::memory_order_release);
    callback.join();
    lease.Reset();
    assert(teardownCalls.load(std::memory_order_acquire) == 1);
    assert(!owner.TryAcquireDispatchLease());
    return 0;
}
