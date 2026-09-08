#include "registry/event_registry.h"
#include "registry/interceptor_registry.h"

#include <cassert>

namespace {

F4ForgeResult F4FORGE_CALL EndpointNoop(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    return F4FORGE_RESULT_SUCCESS;
}

struct EventState {
    uint32_t calls{};
    uint32_t value{};
};

void F4FORGE_CALL OnEvent(
    F4ForgeEventSubscriptionHandle,
    F4ForgeEndpointHandle,
    const void* payload,
    uint32_t payloadSize,
    void* context) noexcept
{
    auto* state = static_cast<EventState*>(context);
    assert(payloadSize == sizeof(uint32_t));
    state->value = *static_cast<const uint32_t*>(payload);
    ++state->calls;
}

F4ForgeResult F4FORGE_CALL OnInterceptor(
    F4ForgeInterceptorSubscriptionHandle,
    F4ForgeEndpointHandle,
    void* payload,
    uint32_t payloadSize,
    void*) noexcept
{
    assert(payloadSize == sizeof(uint32_t));
    ++*static_cast<uint32_t*>(payload);
    return F4FORGE_RESULT_SUCCESS;
}

}

int main()
{
    f4forge::core::EndpointRegistry endpoints;
    f4forge::core::EventRegistry events(endpoints);
    f4forge::core::InterceptorRegistry interceptors(endpoints);
    f4forge::core::EndpointOwner owner;

    F4ForgeEndpointDefinition eventDefinition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_EVENT, 1, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY, 0, 0, sizeof(uint32_t),
        { "test.event", sizeof("test.event") - 1 }, &EndpointNoop, nullptr
    };
    F4ForgeEndpointHandle eventEndpoint = F4FORGE_INVALID_HANDLE;
    assert(endpoints.Register(eventDefinition, &owner, &eventEndpoint) == F4FORGE_RESULT_SUCCESS);

    EventState state{};
    const auto subscription = events.Subscribe(eventEndpoint, &OnEvent, &state);
    assert(subscription != F4FORGE_INVALID_HANDLE);
    uint32_t value = 7;
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 1 && state.value == 7);
    events.Unsubscribe(subscription);
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 1);
    assert(events.Emit(eventEndpoint, &value, 0) == F4FORGE_RESULT_INVALID_REQUEST_SIZE);

    F4ForgeEndpointDefinition interceptorDefinition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_INTERCEPTOR, 1,
        F4FORGE_ENDPOINT_MUTABLE_PAYLOAD, F4FORGE_THREAD_ANY, 0, 0, sizeof(uint32_t),
        { "test.interceptor", sizeof("test.interceptor") - 1 }, &EndpointNoop, nullptr
    };
    F4ForgeEndpointHandle interceptorEndpoint = F4FORGE_INVALID_HANDLE;
    assert(endpoints.Register(interceptorDefinition, &owner, &interceptorEndpoint) == F4FORGE_RESULT_SUCCESS);
    const auto interceptor = interceptors.Intercept(interceptorEndpoint, &OnInterceptor, nullptr);
    assert(interceptor != F4FORGE_INVALID_HANDLE);
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 8);
    interceptors.Remove(interceptor);
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 8);
    return 0;
}
