#include "f4forge_abi.h"

int main(void)
{
    F4ForgeHostApi api = { 0 };
    api.abiVersion = F4FORGE_ABI_VERSION;
    api.structSize = (uint32_t)sizeof(api);
    return api.abiVersion == F4FORGE_ABI_VERSION ? 0 : 1;
}
