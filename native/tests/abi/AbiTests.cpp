#include "f4forge_abi.h"
#include "f4forge_handles.h"

#include <cassert>
#include <cstdint>

int main()
{
    F4ForgeHostApi api{};
    api.abiVersion = F4FORGE_ABI_VERSION;
    api.structSize = sizeof(api);

    assert(api.abiVersion == 1);
    assert(api.structSize == sizeof(F4ForgeHostApi));
    assert(f4forge::HandleGeneration(f4forge::MakeHandle(42, 9)) == 42);
    assert(f4forge::HandleIndex(f4forge::MakeHandle(42, 9)) == 9);
    assert(F4FORGE_INVALID_HANDLE == 0);
    return 0;
}
