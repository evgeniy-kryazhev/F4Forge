#include "f4forge_abi.h"

int main(void)
{
    F4ForgeHostApi api = { 0 };
    F4ForgeAsyncOperationState state = F4FORGE_ASYNC_OPERATION_PENDING;
    api.abiVersion = F4FORGE_ABI_VERSION;
    api.structSize = (uint32_t)sizeof(api);
    (void)state;
    return api.abiVersion == F4FORGE_ABI_VERSION && api.structSize == sizeof(api) ? 0 : 1;
}
