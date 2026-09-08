#include "f4forge_host.h"

namespace f4forge::core {

F4ForgeHost& F4ForgeHost::Instance() noexcept
{
    static F4ForgeHost host;
    return host;
}

F4ForgeHost::F4ForgeHost() noexcept : _events(_endpoints), _interceptors(_endpoints), _modules(_endpoints)
{
    _api.abiVersion = F4FORGE_ABI_VERSION;
    _api.structSize = sizeof(F4ForgeHostApi);
    _api.resolveEndpoint = &ResolveEndpoint;
    _api.invoke = &Invoke;
    _api.subscribe = &Subscribe;
    _api.unsubscribe = &Unsubscribe;
    _api.intercept = &Intercept;
    _api.removeInterceptor = &RemoveInterceptor;
    _api.registerEndpoint = &RegisterEndpoint;
    _api.registerModule = &RegisterModule;
    _api.unregisterModule = &UnregisterModule;
    _api.queryCapability = &QueryCapability;
    _api.queueTask = &QueueTask;
    _api.log = &Log;
}

const F4ForgeHostApi& F4ForgeHost::Api() const noexcept
{
    return _api;
}

EndpointRegistry& F4ForgeHost::Endpoints() noexcept
{
    return _endpoints;
}

EventRegistry& F4ForgeHost::Events() noexcept
{
    return _events;
}

InterceptorRegistry& F4ForgeHost::Interceptors() noexcept
{
    return _interceptors;
}

ModuleManager& F4ForgeHost::Modules() noexcept
{
    return _modules;
}

F4ForgeEndpointHandle F4FORGE_CALL F4ForgeHost::ResolveEndpoint(
    F4ForgeStringView name,
    uint32_t version) F4FORGE_NOEXCEPT
{
    return Instance()._endpoints.Resolve(name, version);
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::Invoke(
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT
{
    return Instance()._endpoints.Invoke(endpoint, request, requestSize, response, responseCapacity, responseSize);
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::RegisterEndpoint(
    F4ForgeModuleHandle module,
    const F4ForgeEndpointDefinition* definition,
    F4ForgeEndpointHandle* endpoint) F4FORGE_NOEXCEPT
{
    if (definition == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    auto& host = Instance();
    return host._endpoints.Register(*definition, host._modules.Owner(module), endpoint);
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::RegisterModule(
    F4ForgeRuntimeHandle runtime,
    F4ForgeStringView id,
    uint32_t version,
    F4ForgeModuleHandle* module) F4FORGE_NOEXCEPT
{
    return Instance()._modules.Register(runtime, id, version, module);
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::UnregisterModule(F4ForgeModuleHandle module) F4FORGE_NOEXCEPT
{
    return Instance()._modules.Unregister(module);
}

uint32_t F4FORGE_CALL F4ForgeHost::QueryCapability(
    F4ForgeStringView,
    uint32_t) F4FORGE_NOEXCEPT
{
    return 0;
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::QueueTask(
    F4ForgeRuntimeHandle,
    uint64_t,
    void*) F4FORGE_NOEXCEPT
{
    return F4FORGE_RESULT_RUNTIME_UNAVAILABLE;
}

void F4FORGE_CALL F4ForgeHost::Log(uint32_t, F4ForgeStringView) F4FORGE_NOEXCEPT
{
}

F4ForgeEventSubscriptionHandle F4FORGE_CALL F4ForgeHost::Subscribe(
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context) F4FORGE_NOEXCEPT
{
    return Instance()._events.Subscribe(endpoint, callback, context);
}

void F4FORGE_CALL F4ForgeHost::Unsubscribe(F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT
{
    Instance()._events.Unsubscribe(subscription);
}

F4ForgeInterceptorSubscriptionHandle F4FORGE_CALL F4ForgeHost::Intercept(
    F4ForgeEndpointHandle endpoint,
    F4ForgeInterceptorCallback callback,
    void* context) F4FORGE_NOEXCEPT
{
    return Instance()._interceptors.Intercept(endpoint, callback, context);
}

void F4FORGE_CALL F4ForgeHost::RemoveInterceptor(F4ForgeInterceptorSubscriptionHandle subscription) F4FORGE_NOEXCEPT
{
    Instance()._interceptors.Remove(subscription);
}

}
