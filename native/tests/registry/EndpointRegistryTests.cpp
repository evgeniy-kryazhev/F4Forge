#include "registry/endpoint_registry.h"

#include <cassert>
#include <cstdint>

namespace {

struct Request {
    uint32_t value;
};

struct Response {
    uint32_t value;
};

F4ForgeResult F4FORGE_CALL Echo(
    void*,
    const void* request,
    uint32_t requestSize,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) noexcept
{
    if (requestSize != sizeof(Request) || responseCapacity < sizeof(Response))
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    static_cast<Response*>(response)->value = static_cast<const Request*>(request)->value;
    if (responseSize != nullptr) *responseSize = sizeof(Response);
    return F4FORGE_RESULT_SUCCESS;
}

int32_t F4FORGE_CALL AllowGameThread() noexcept { return 1; }
int32_t F4FORGE_CALL DenyGameThread() noexcept { return 0; }

}

int main()
{
    f4forge::core::EndpointRegistry registry;
    f4forge::core::EndpointOwner owner;
    F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_METHOD,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY,
        sizeof(Request),
        sizeof(Response),
        0,
        { "test.echo", 10 },
        &Echo,
        nullptr
    };

    F4ForgeEndpointHandle endpoint = F4FORGE_INVALID_HANDLE;
    const auto registerResult = registry.Register(definition, &owner, &endpoint);
    assert(registerResult == F4FORGE_RESULT_SUCCESS);
    assert(endpoint != F4FORGE_INVALID_HANDLE);
    assert(registry.Resolve({ "test.echo", 10 }, 1) == endpoint);

    const Request request{ 42 };
    Response response{};
    uint32_t responseSize = 0;
    assert(registry.Invoke(endpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_SUCCESS);
    assert(response.value == 42);
    assert(responseSize == sizeof(Response));
    assert(registry.Invoke(endpoint, &request, 0, &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_INVALID_REQUEST_SIZE);
    assert(registry.Invoke(endpoint, &request, sizeof(request), nullptr, 0, &responseSize)
        == F4FORGE_RESULT_INVALID_RESPONSE_CAPACITY);

    registry.InvalidateOwner(&owner);
    assert(registry.Invoke(endpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_STALE_HANDLE);
    assert(registry.Resolve({ "test.echo", 10 }, 1) == F4FORGE_INVALID_HANDLE);

    f4forge::core::EndpointOwner reusedOwner;
    F4ForgeEndpointHandle reusedEndpoint = F4FORGE_INVALID_HANDLE;
    const auto reusedRegisterResult = registry.Register(definition, &reusedOwner, &reusedEndpoint);
    assert(reusedRegisterResult == F4FORGE_RESULT_SUCCESS);
    assert(f4forge::HandleIndex(reusedEndpoint) == f4forge::HandleIndex(endpoint));
    assert(f4forge::HandleGeneration(reusedEndpoint) != f4forge::HandleGeneration(endpoint));
    assert(registry.Invoke(endpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_STALE_HANDLE);

    f4forge::core::EndpointOwner gameOwner;
    F4ForgeEndpointDefinition gameDefinition{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_METHOD, 1, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY, sizeof(Request), sizeof(Response), 0,
        { "test.game", sizeof("test.game") - 1 }, &Echo, nullptr
    };
    F4ForgeEndpointHandle gameEndpoint = F4FORGE_INVALID_HANDLE;
    const auto gameRegisterResult = registry.Register(gameDefinition, &gameOwner, &gameEndpoint);
    assert(gameRegisterResult == F4FORGE_RESULT_SUCCESS);
    registry.SetGameThreadCheck(nullptr);
    assert(registry.Invoke(gameEndpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_WRONG_THREAD);
    registry.SetGameThreadCheck(&AllowGameThread);
    assert(registry.Invoke(gameEndpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_SUCCESS);
    registry.SetGameThreadCheck(&DenyGameThread);
    assert(registry.Invoke(gameEndpoint, &request, sizeof(request), &response, sizeof(response), &responseSize)
        == F4FORGE_RESULT_WRONG_THREAD);

    return 0;
}
