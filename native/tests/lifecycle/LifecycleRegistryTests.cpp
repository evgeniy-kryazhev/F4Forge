#include "lifecycle/lifecycle_registry.h"
#include "test_harness.h"

#include <atomic>
#include <chrono>
#include <thread>

int main()
{
    f4forge::test::Context test;
    f4forge::core::LifecycleRegistry registry;
    unsigned calls = 0;

    F4FORGE_CHECK(test, !registry.Invoke([&] { ++calls; }));
    registry.Start();
    registry.Start();
    F4FORGE_CHECK(test, registry.Invoke([&] { ++calls; }));

    std::atomic<bool> entered{};
    std::atomic<bool> release{};
    std::atomic<bool> stopped{};
    std::thread callback([&] {
        registry.Invoke([&] {
            entered.store(true);
            while (!release.load()) std::this_thread::yield();
            ++calls;
        });
    });
    while (!entered.load()) std::this_thread::yield();
    std::thread stop([&] { registry.Stop(); stopped.store(true); });
    std::this_thread::sleep_for(std::chrono::milliseconds(10));
    F4FORGE_CHECK(test, !stopped.load());
    release.store(true);
    callback.join();
    stop.join();

    F4FORGE_CHECK(test, !registry.Invoke([&] { ++calls; }));
    registry.Stop();
    F4FORGE_CHECK(test, calls == 2);
    return test.Failures() == 0 ? 0 : 1;
}
