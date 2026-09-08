#include "registry/capability_registry.h"

#include <cassert>

int main()
{
    f4forge::core::CapabilityRegistry capabilities;
    f4forge::core::EndpointOwner owner;
    const auto registerResult = capabilities.Register({ "core.test", sizeof("core.test") - 1 }, 2, &owner);
    assert(registerResult == F4FORGE_RESULT_SUCCESS);
    assert(capabilities.Query({ "core.test", sizeof("core.test") - 1 }, 1) == 2);
    assert(capabilities.Query({ "core.test", sizeof("core.test") - 1 }, 3) == 0);
    const auto duplicateRegisterResult = capabilities.Register(
        { "core.test", sizeof("core.test") - 1 }, 2, &owner);
    assert(duplicateRegisterResult == F4FORGE_RESULT_ALREADY_REGISTERED);
    owner.active.store(false, std::memory_order_release);
    assert(capabilities.Query({ "core.test", sizeof("core.test") - 1 }, 1) == 0);
    capabilities.InvalidateOwner(&owner);
    return 0;
}
