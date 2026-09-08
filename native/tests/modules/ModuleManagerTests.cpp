#include "modules/module_manager.h"
#include "registry/capability_registry.h"
#include "registry/event_registry.h"
#include "registry/interceptor_registry.h"

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
    f4forge::core::EventRegistry events(endpoints);
    f4forge::core::InterceptorRegistry interceptors(endpoints);
    f4forge::core::CapabilityRegistry capabilities;
    f4forge::core::ModuleManager modules(endpoints, events, interceptors, capabilities);

    F4ForgeModuleHandle module = F4FORGE_INVALID_HANDLE;
    const auto registerResult = modules.Register(7, { "test.module", sizeof("test.module") - 1 }, 1, &module);
    assert(registerResult == F4FORGE_RESULT_SUCCESS);
    assert(modules.IsActive(module));
    const auto duplicateRegisterResult = modules.Register(
        7, { "test.module", sizeof("test.module") - 1 }, 1, &module);
    assert(duplicateRegisterResult == F4FORGE_RESULT_ALREADY_REGISTERED);

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
    const auto endpointRegisterResult = endpoints.Register(definition, modules.Owner(module), &endpoint);
    assert(endpointRegisterResult == F4FORGE_RESULT_SUCCESS);

    const auto unregisterResult = modules.Unregister(module);
    assert(unregisterResult == F4FORGE_RESULT_SUCCESS);
    assert(!modules.IsActive(module));
    const auto duplicateUnregisterResult = modules.Unregister(module);
    assert(duplicateUnregisterResult == F4FORGE_RESULT_INVALID_HANDLE);
    assert(endpoints.Invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_STALE_HANDLE);
    F4ForgeModuleHandle reusedModule = F4FORGE_INVALID_HANDLE;
    const auto reusedRegisterResult = modules.Register(
        7, { "test.module", sizeof("test.module") - 1 }, 1, &reusedModule);
    assert(reusedRegisterResult == F4FORGE_RESULT_SUCCESS);
    assert(f4forge::HandleIndex(reusedModule) == f4forge::HandleIndex(module));
    assert(f4forge::HandleGeneration(reusedModule) != f4forge::HandleGeneration(module));
    const auto reusedUnregisterResult = modules.Unregister(reusedModule);
    assert(reusedUnregisterResult == F4FORGE_RESULT_SUCCESS);
    return 0;
}
