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
    f4forge::core::EndpointOwner subscriberOwner;
    f4forge::core::EndpointOwner interceptorOwner;

    F4ForgeEndpointDefinition eventDefinition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_EVENT, 1, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY, 0, 0, sizeof(uint32_t),
        { "test.event", sizeof("test.event") - 1 }, &EndpointNoop, nullptr
    };
    F4ForgeEndpointHandle eventEndpoint = F4FORGE_INVALID_HANDLE;
    const auto eventRegisterResult = endpoints.Register(eventDefinition, &owner, &eventEndpoint);
    assert(eventRegisterResult == F4FORGE_RESULT_SUCCESS);

    EventState state{};
    const auto subscription = events.Subscribe(&subscriberOwner, eventEndpoint, &OnEvent, &state);
    assert(subscription != F4FORGE_INVALID_HANDLE);
    uint32_t value = 7;
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 1 && state.value == 7);
    events.Unsubscribe(subscription);
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 1);
    const auto reusedSubscription = events.Subscribe(&subscriberOwner, eventEndpoint, &OnEvent, &state);
    assert(reusedSubscription != F4FORGE_INVALID_HANDLE);
    assert(f4forge::HandleIndex(reusedSubscription) == f4forge::HandleIndex(subscription));
    assert(f4forge::HandleGeneration(reusedSubscription) != f4forge::HandleGeneration(subscription));
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 2);
    events.Unsubscribe(reusedSubscription);
    assert(events.Emit(eventEndpoint, &value, 0) == F4FORGE_RESULT_INVALID_REQUEST_SIZE);
    subscriberOwner.BeginQuiescing();
    assert(events.Emit(eventEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(state.calls == 2);

    F4ForgeEndpointDefinition interceptorDefinition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_INTERCEPTOR, 1,
        F4FORGE_ENDPOINT_MUTABLE_PAYLOAD, F4FORGE_THREAD_ANY, 0, 0, sizeof(uint32_t),
        { "test.interceptor", sizeof("test.interceptor") - 1 }, &EndpointNoop, nullptr
    };
    F4ForgeEndpointHandle interceptorEndpoint = F4FORGE_INVALID_HANDLE;
    const auto interceptorRegisterResult = endpoints.Register(interceptorDefinition, &owner, &interceptorEndpoint);
    assert(interceptorRegisterResult == F4FORGE_RESULT_SUCCESS);
    const auto interceptor = interceptors.Intercept(&interceptorOwner, interceptorEndpoint, &OnInterceptor, nullptr);
    assert(interceptor != F4FORGE_INVALID_HANDLE);
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 8);
    interceptors.Remove(interceptor);
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 8);
    const auto reusedInterceptor = interceptors.Intercept(
        &interceptorOwner, interceptorEndpoint, &OnInterceptor, nullptr);
    assert(reusedInterceptor != F4FORGE_INVALID_HANDLE);
    assert(f4forge::HandleIndex(reusedInterceptor) == f4forge::HandleIndex(interceptor));
    assert(f4forge::HandleGeneration(reusedInterceptor) != f4forge::HandleGeneration(interceptor));
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 9);
    interceptorOwner.BeginQuiescing();
    assert(interceptors.Emit(interceptorEndpoint, &value, sizeof(value)) == F4FORGE_RESULT_SUCCESS);
    assert(value == 9);
    return 0;
}
