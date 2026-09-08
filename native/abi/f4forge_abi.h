#pragma once

#include "f4forge_results.h"
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_MSC_VER)
#define F4FORGE_CALL __cdecl
#else
#define F4FORGE_CALL
#endif

#ifdef __cplusplus
#define F4FORGE_NOEXCEPT noexcept
#else
#define F4FORGE_NOEXCEPT
#endif

#define F4FORGE_ABI_VERSION 1u
#define F4FORGE_RUNTIME_PROVIDER_ABI_VERSION 1u
#define F4FORGE_INVALID_HANDLE UINT64_C(0)

#define F4FORGE_HAS_FIELD(structSize, structType, field) \
    ((structSize) >= (uint32_t)(offsetof(structType, field) + sizeof(((structType*)0)->field)))

typedef uint64_t F4ForgeRawHandle;
typedef F4ForgeRawHandle F4ForgeEndpointHandle;
typedef F4ForgeRawHandle F4ForgeEventSubscriptionHandle;
typedef F4ForgeRawHandle F4ForgeInterceptorSubscriptionHandle;
typedef F4ForgeRawHandle F4ForgeModuleHandle;
typedef F4ForgeRawHandle F4ForgeRuntimeHandle;
typedef F4ForgeRawHandle F4ForgePluginHandle;

typedef struct F4ForgeStringView {
    const char* data;
    uint32_t length;
} F4ForgeStringView;

typedef struct F4ForgeByteView {
    const uint8_t* data;
    uint32_t length;
} F4ForgeByteView;

typedef enum F4ForgeEndpointKind {
    F4FORGE_ENDPOINT_METHOD = 1,
    F4FORGE_ENDPOINT_EVENT = 2,
    F4FORGE_ENDPOINT_INTERCEPTOR = 3
} F4ForgeEndpointKind;

typedef enum F4ForgeThreadPolicy {
    F4FORGE_THREAD_ANY = 0,
    F4FORGE_THREAD_GAME_ONLY = 1
} F4ForgeThreadPolicy;

typedef enum F4ForgeEndpointFlags {
    F4FORGE_ENDPOINT_NONE = 0,
    F4FORGE_ENDPOINT_OPTIONAL = 1u << 0,
    F4FORGE_ENDPOINT_EXPERIMENTAL = 1u << 1,
    F4FORGE_ENDPOINT_MUTABLE_PAYLOAD = 1u << 2
} F4ForgeEndpointFlags;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeEndpointThunk)(
    void* context,
    const void* request,
    uint32_t requestSize,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeEventCallback)(
    F4ForgeEventSubscriptionHandle subscription,
    F4ForgeEndpointHandle endpoint,
    const void* payload,
    uint32_t payloadSize,
    void* context) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeInterceptorCallback)(
    F4ForgeInterceptorSubscriptionHandle subscription,
    F4ForgeEndpointHandle endpoint,
    void* payload,
    uint32_t payloadSize,
    void* context) F4FORGE_NOEXCEPT;

typedef struct F4ForgeEndpointDefinition {
    uint32_t structSize;
    uint32_t kind;
    uint32_t version;
    uint32_t flags;
    uint32_t threadPolicy;
    uint32_t requestSize;
    uint32_t responseSize;
    uint32_t payloadSize;
    F4ForgeStringView name;
    F4ForgeEndpointThunk thunk;
    void* context;
} F4ForgeEndpointDefinition;

typedef void (F4FORGE_CALL* F4ForgeLogFn)(
    uint32_t level,
    F4ForgeStringView message) F4FORGE_NOEXCEPT;

typedef F4ForgeEndpointHandle (F4FORGE_CALL* F4ForgeResolveEndpointFn)(
    F4ForgeStringView name,
    uint32_t version) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeInvokeFn)(
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT;

typedef F4ForgeEventSubscriptionHandle (F4FORGE_CALL* F4ForgeSubscribeFn)(
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeUnsubscribeFn)(
    F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT;

typedef F4ForgeInterceptorSubscriptionHandle (F4FORGE_CALL* F4ForgeInterceptFn)(
    F4ForgeEndpointHandle endpoint,
    F4ForgeInterceptorCallback callback,
    void* context) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeRemoveInterceptorFn)(
    F4ForgeInterceptorSubscriptionHandle subscription) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeRegisterEndpointFn)(
    F4ForgeModuleHandle module,
    const F4ForgeEndpointDefinition* definition,
    F4ForgeEndpointHandle* endpoint) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeRegisterModuleFn)(
    F4ForgeRuntimeHandle runtime,
    F4ForgeStringView id,
    uint32_t version,
    F4ForgeModuleHandle* module) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeUnregisterModuleFn)(
    F4ForgeModuleHandle module) F4FORGE_NOEXCEPT;

typedef uint32_t (F4FORGE_CALL* F4ForgeQueryCapabilityFn)(
    F4ForgeStringView id,
    uint32_t minimumVersion) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeQueueTaskFn)(
    F4ForgeRuntimeHandle runtime,
    uint64_t taskHandle,
    void* context) F4FORGE_NOEXCEPT;

typedef struct F4ForgeHostApi {
    uint32_t abiVersion;
    uint32_t structSize;
    F4ForgeResolveEndpointFn resolveEndpoint;
    F4ForgeInvokeFn invoke;
    F4ForgeSubscribeFn subscribe;
    F4ForgeUnsubscribeFn unsubscribe;
    F4ForgeInterceptFn intercept;
    F4ForgeRemoveInterceptorFn removeInterceptor;
    F4ForgeRegisterEndpointFn registerEndpoint;
    F4ForgeRegisterModuleFn registerModule;
    F4ForgeUnregisterModuleFn unregisterModule;
    F4ForgeQueryCapabilityFn queryCapability;
    F4ForgeQueueTaskFn queueTask;
    F4ForgeLogFn log;
} F4ForgeHostApi;

typedef const F4ForgeHostApi* (F4FORGE_CALL* F4ForgeGetHostApiFn)(void) F4FORGE_NOEXCEPT;

#ifdef __cplusplus
}

#include <cstddef>
#include <type_traits>

static_assert(sizeof(void*) == 8, "F4Forge requires x64");
static_assert(std::is_standard_layout_v<F4ForgeStringView>);
static_assert(std::is_trivial_v<F4ForgeStringView>);
static_assert(std::is_standard_layout_v<F4ForgeEndpointDefinition>);
static_assert(std::is_trivial_v<F4ForgeEndpointDefinition>);
static_assert(std::is_standard_layout_v<F4ForgeHostApi>);
static_assert(std::is_trivial_v<F4ForgeHostApi>);
static_assert(sizeof(F4ForgeStringView) == 16);
static_assert(sizeof(F4ForgeByteView) == 16);
static_assert(offsetof(F4ForgeHostApi, abiVersion) == 0);
static_assert(offsetof(F4ForgeHostApi, structSize) == 4);
static_assert(offsetof(F4ForgeHostApi, resolveEndpoint) == 8);
static_assert(offsetof(F4ForgeHostApi, invoke) == 16);
static_assert(sizeof(F4ForgeHostApi) == 104);
static_assert(offsetof(F4ForgeEndpointDefinition, name) == 32);
static_assert(offsetof(F4ForgeEndpointDefinition, thunk) == 48);
static_assert(sizeof(F4ForgeEndpointDefinition) == 64);
#endif
