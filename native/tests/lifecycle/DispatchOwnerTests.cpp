#include "lifecycle/dispatch_owner.h"
#include "test_harness.h"

#include <chrono>

int main()
{
    f4forge::test::Context test;
    f4forge::core::DispatchOwner owner;
    auto outerLease = owner.TryAcquireDispatchLease();
    auto nestedLease = owner.TryAcquireDispatchLease();
    F4FORGE_CHECK(test, outerLease);
    F4FORGE_CHECK(test, nestedLease);

    uint32_t teardownCalls = 0;
    owner.BeginQuiescing([&] { ++teardownCalls; });
    F4FORGE_CHECK(test, !owner.TryAcquireDispatchLease());
    F4FORGE_CHECK(test, !owner.WaitForQuiescence(std::chrono::milliseconds(10)));
    F4FORGE_CHECK(test, owner.IsQuarantined());

    nestedLease.Reset();
    outerLease.Reset();
    F4FORGE_CHECK(test, teardownCalls == 1);
    F4FORGE_CHECK(test, !owner.TryAcquireDispatchLease());
    return test.Failures() == 0 ? 0 : 1;
}
