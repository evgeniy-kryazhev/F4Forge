#include "f4forge_runtime_abi.h"

namespace {

F4ForgeResult F4FORGE_CALL Initialize(const F4ForgeRuntimeInitializeParams*) F4FORGE_NOEXCEPT
{
    return F4FORGE_RESULT_SUCCESS;
}

void F4FORGE_CALL Shutdown(F4ForgeRuntimeHandle) F4FORGE_NOEXCEPT {}
void F4FORGE_CALL ExecuteTask(const F4ForgeRuntimeTask*) F4FORGE_NOEXCEPT {}

const F4ForgeRuntimeInfo Info{
    F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
    sizeof(F4ForgeRuntimeInfo),
    { "test-b", sizeof("test-b") - 1 },
    { "Test B", sizeof("Test B") - 1 },
    1
};

const F4ForgeRuntimeProvider Provider{
    F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
    sizeof(F4ForgeRuntimeProvider),
    &Info,
    &Initialize,
    &Shutdown,
    &ExecuteTask
};

}

extern "C" __declspec(dllexport) const F4ForgeRuntimeProvider* F4FORGE_CALL F4ForgeDescribeRuntime() F4FORGE_NOEXCEPT
{
    return &Provider;
}
