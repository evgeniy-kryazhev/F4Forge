#include "modules/module_manager.h"

#include <cassert>

namespace {

struct Request {
    uint32_t value;
};

F4ForgeResult F4FORGE_CALL Noop(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    return F4FORGE_RESULT_SUCCESS;
}

}

int main()
{
    f4forge::core::EndpointRegistry endpoints;
    f4forge::core::ModuleManager modules(endpoints);

    F4ForgeModuleHandle module = F4FORGE_INVALID_HANDLE;
    assert(modules.Register(7, { "test.module", sizeof("test.module") - 1 }, 1, &module)
        == F4FORGE_RESULT_SUCCESS);
    assert(modules.IsActive(module));
    assert(modules.Register(7, { "test.module", sizeof("test.module") - 1 }, 1, &module)
        == F4FORGE_RESULT_ALREADY_REGISTERED);

    F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_METHOD,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY,
        sizeof(Request),
        0,
        0,
        { "test.module.noop", sizeof("test.module.noop") - 1 },
        &Noop,
        nullptr
    };
    F4ForgeEndpointHandle endpoint = F4FORGE_INVALID_HANDLE;
    assert(endpoints.Register(definition, modules.Owner(module), &endpoint) == F4FORGE_RESULT_SUCCESS);

    assert(modules.Unregister(module) == F4FORGE_RESULT_SUCCESS);
    assert(!modules.IsActive(module));
    assert(modules.Unregister(module) == F4FORGE_RESULT_INVALID_HANDLE);
    assert(endpoints.Invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_STALE_HANDLE);
    return 0;
}
