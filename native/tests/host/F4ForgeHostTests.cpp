#include "f4forge_host.h"

#include <cassert>

namespace {

F4ForgeResult F4FORGE_CALL Success(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    return F4FORGE_RESULT_SUCCESS;
}

}

int main()
{
    const auto& api = f4forge::core::F4ForgeHost::Instance().Api();
    assert(api.abiVersion == F4FORGE_ABI_VERSION);
    assert(api.structSize == sizeof(F4ForgeHostApi));
    assert(api.resolveEndpoint != nullptr);
    assert(api.invoke != nullptr);
    assert(api.registerEndpoint != nullptr);
    assert(api.registerModule != nullptr);
    assert(api.unregisterModule != nullptr);

    F4ForgeModuleHandle module = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(1, { "host.test", sizeof("host.test") - 1 }, 1, &module)
        == F4FORGE_RESULT_SUCCESS);

    const F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_METHOD,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY,
        0,
        0,
        0,
        { "host.test.success", sizeof("host.test.success") - 1 },
        &Success,
        nullptr
    };
    F4ForgeEndpointHandle endpoint = F4FORGE_INVALID_HANDLE;
    assert(api.registerEndpoint(module, &definition, &endpoint) == F4FORGE_RESULT_SUCCESS);
    assert(api.resolveEndpoint({ "host.test.success", sizeof("host.test.success") - 1 }, 1) == endpoint);
    assert(api.invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_SUCCESS);
    assert(api.subscribe(endpoint, nullptr, nullptr) == F4FORGE_INVALID_HANDLE);
    assert(api.queueTask(1, 1, nullptr) == F4FORGE_RESULT_RUNTIME_UNAVAILABLE);
    assert(api.unregisterModule(module) == F4FORGE_RESULT_SUCCESS);
    assert(api.invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_STALE_HANDLE);
    return 0;
}
