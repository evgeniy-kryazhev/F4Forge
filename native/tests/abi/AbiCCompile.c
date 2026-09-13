#include "f4forge_abi.h"
#include "f4forge_runtime_abi.h"

#define CHECK(value) do { if (!(value)) return 1; } while (0)
#define C_STATIC_ASSERT(value, name) typedef char static_assert_##name[(value) ? 1 : -1]

C_STATIC_ASSERT(sizeof(F4ForgeKeyEventData) == 28, key_event_size);
C_STATIC_ASSERT(offsetof(F4ForgeKeyEventData, keyCode) == 8, key_event_key_offset);
C_STATIC_ASSERT(F4FORGE_KEY_A == 0x41, key_a_value);
C_STATIC_ASSERT(F4FORGE_KEY_APOSTROPHE == 0xDE, key_apostrophe_value);

static F4ForgeResult F4FORGE_CALL StubInitialize(const F4ForgeRuntimeInitializeParams* params)
{
    return params == 0 ? F4FORGE_RESULT_INVALID_ARGUMENT : F4FORGE_RESULT_SUCCESS;
}

static void F4FORGE_CALL StubShutdown(F4ForgeRuntimeHandle runtime) { (void)runtime; }
static void F4FORGE_CALL StubTask(F4ForgeRuntimeHandle runtime, uint64_t taskHandle)
{
    (void)runtime;
    (void)taskHandle;
}

int main(void)
{
    F4ForgeHostApi api = { 0 };
    F4ForgeAsyncOperationState state = F4FORGE_ASYNC_OPERATION_PENDING;
    F4ForgeRuntimeInitializeParams params = { 0 };
    F4ForgeRuntimeProvider provider = { 0 };
    api.abiVersion = F4FORGE_ABI_VERSION;
    api.structSize = (uint32_t)sizeof(api);
    provider.initialize = StubInitialize;
    provider.shutdown = StubShutdown;
    provider.executeTask = StubTask;
    params.abiVersion = F4FORGE_RUNTIME_PROVIDER_ABI_VERSION;
    params.structSize = (uint32_t)sizeof(params);
    CHECK(api.abiVersion == F4FORGE_ABI_VERSION);
    CHECK(api.structSize == sizeof(api));
    CHECK(sizeof(F4ForgeResult) == 4 && sizeof(F4ForgeRawHandle) == 8);
    CHECK(sizeof(F4ForgeRuntimeInitializeParams) == 56);
    CHECK(provider.initialize(&params) == F4FORGE_RESULT_SUCCESS);
    (void)state;
    return 0;
}
