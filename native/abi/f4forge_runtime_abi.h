#pragma once

#include "f4forge_abi.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef struct F4ForgeRuntimeInfo {
    uint32_t abiVersion;
    uint32_t structSize;
    F4ForgeStringView id;
    F4ForgeStringView name;
    uint32_t providerVersion;
} F4ForgeRuntimeInfo;

typedef struct F4ForgeRuntimeInitializeParams {
    uint32_t abiVersion;
    uint32_t structSize;
    const F4ForgeHostApi* host;
    F4ForgeRuntimeHandle runtime;
    F4ForgeStringView pluginDirectory;
    F4ForgeStringView configDirectory;
} F4ForgeRuntimeInitializeParams;

typedef struct F4ForgeRuntimeTask {
    void* context;
    uint64_t taskHandle;
} F4ForgeRuntimeTask;

typedef struct F4ForgeManagedBootstrapArgs {
    uint32_t abiVersion;
    uint32_t structSize;
    const F4ForgeHostApi* host;
    F4ForgeStringView pluginDirectory;
    F4ForgeStringView configDirectory;
} F4ForgeManagedBootstrapArgs;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeRuntimeInitializeFn)(
    const F4ForgeRuntimeInitializeParams* params) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeRuntimeShutdownFn)(
    F4ForgeRuntimeHandle runtime) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeRuntimeExecuteTaskFn)(
    const F4ForgeRuntimeTask* task) F4FORGE_NOEXCEPT;

typedef struct F4ForgeRuntimeProvider {
    uint32_t abiVersion;
    uint32_t structSize;
    const F4ForgeRuntimeInfo* info;
    F4ForgeRuntimeInitializeFn initialize;
    F4ForgeRuntimeShutdownFn shutdown;
    F4ForgeRuntimeExecuteTaskFn executeTask;
} F4ForgeRuntimeProvider;

typedef const F4ForgeRuntimeProvider* (F4FORGE_CALL* F4ForgeDescribeRuntimeFn)(void) F4FORGE_NOEXCEPT;

#ifdef __cplusplus
}

#include <cstddef>
#include <type_traits>

static_assert(std::is_standard_layout_v<F4ForgeRuntimeInfo>);
static_assert(std::is_trivial_v<F4ForgeRuntimeInfo>);
static_assert(std::is_standard_layout_v<F4ForgeRuntimeInitializeParams>);
static_assert(std::is_trivial_v<F4ForgeRuntimeInitializeParams>);
static_assert(std::is_standard_layout_v<F4ForgeRuntimeTask>);
static_assert(std::is_trivial_v<F4ForgeRuntimeTask>);
static_assert(std::is_standard_layout_v<F4ForgeManagedBootstrapArgs>);
static_assert(std::is_trivial_v<F4ForgeManagedBootstrapArgs>);
static_assert(std::is_standard_layout_v<F4ForgeRuntimeProvider>);
static_assert(std::is_trivial_v<F4ForgeRuntimeProvider>);
static_assert(sizeof(F4ForgeRuntimeInfo) == 48);
static_assert(sizeof(F4ForgeRuntimeInitializeParams) == 56);
static_assert(sizeof(F4ForgeRuntimeTask) == 16);
static_assert(sizeof(F4ForgeManagedBootstrapArgs) == 48);
static_assert(sizeof(F4ForgeRuntimeProvider) == 40);
static_assert(offsetof(F4ForgeRuntimeProvider, info) == 8);
static_assert(offsetof(F4ForgeRuntimeProvider, initialize) == 16);
#endif
