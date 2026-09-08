#include "modules/module_manager.h"
#include "registry/capability_registry.h"
#include "registry/event_registry.h"
#include "registry/interceptor_registry.h"

#include <cassert>

namespace {

struct Request {
    uint32_t value;
};

struct SelfUnregisterContext {
    f4forge::core::ModuleManager* modules{};
    F4ForgeModuleHandle module{};
    uint32_t calls{};
};

F4ForgeResult F4FORGE_CALL Noop(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult F4FORGE_CALL SelfUnregister(
    void* context, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    auto& self = *static_cast<SelfUnregisterContext*>(context);
    ++self.calls;
    const auto result = self.modules->Unregister(self.module);
    assert(result == F4FORGE_RESULT_SUCCESS);
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

    F4ForgeModuleHandle reentrantModule = F4FORGE_INVALID_HANDLE;
    const auto reentrantRegisterResult = modules.Register(
        7, { "reentrant.module", sizeof("reentrant.module") - 1 }, 1, &reentrantModule);
    assert(reentrantRegisterResult == F4FORGE_RESULT_SUCCESS);
    SelfUnregisterContext reentrantContext{ &modules, reentrantModule };
    F4ForgeEndpointDefinition reentrantDefinition = definition;
    reentrantDefinition.name = { "reentrant.module.endpoint", sizeof("reentrant.module.endpoint") - 1 };
    reentrantDefinition.thunk = &SelfUnregister;
    reentrantDefinition.context = &reentrantContext;
    F4ForgeEndpointHandle reentrantEndpoint = F4FORGE_INVALID_HANDLE;
    const auto reentrantEndpointResult = endpoints.Register(
        reentrantDefinition, modules.Owner(reentrantModule), &reentrantEndpoint);
    assert(reentrantEndpointResult == F4FORGE_RESULT_SUCCESS);
    Request reentrantRequest{};
    assert(endpoints.Invoke(
        reentrantEndpoint, &reentrantRequest, sizeof(reentrantRequest), nullptr, 0, nullptr)
        == F4FORGE_RESULT_SUCCESS);
    assert(reentrantContext.calls == 1);
    assert(!modules.IsActive(reentrantModule));
    assert(endpoints.Invoke(reentrantEndpoint, nullptr, 0, nullptr, 0, nullptr)
        == F4FORGE_RESULT_STALE_HANDLE);
    return 0;
}
