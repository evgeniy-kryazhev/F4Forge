#include "f4forge_abi.h"
#include "f4forge_handles.h"
#include "f4forge_runtime_abi.h"

#include <cstdint>

#define CHECK(value) do { if (!(value)) return 1; } while (false)

int main()
{
    F4ForgeHostApi api{};
    api.abiVersion = F4FORGE_ABI_VERSION;
    api.structSize = sizeof(api);

    CHECK(api.abiVersion == F4FORGE_ABI_VERSION);
    CHECK(api.structSize == sizeof(F4ForgeHostApi));
    CHECK(f4forge::HandleGeneration(f4forge::MakeHandle(42, 9)) == 42);
    CHECK(f4forge::HandleIndex(f4forge::MakeHandle(42, 9)) == 9);
    CHECK(F4FORGE_INVALID_HANDLE == 0);
    CHECK(sizeof(F4ForgeRuntimeInitializeParams) == 56);
    CHECK(sizeof(F4ForgeManagedBootstrapArgs) == 56);
    return 0;
}
