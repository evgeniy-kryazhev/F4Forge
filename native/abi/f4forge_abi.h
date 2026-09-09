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

#define F4FORGE_ABI_VERSION 2u
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
typedef F4ForgeRawHandle F4ForgeAsyncOperationHandle;

typedef uint32_t F4ForgeAsyncOperationState;
#define F4FORGE_ASYNC_OPERATION_PENDING 0u
#define F4FORGE_ASYNC_OPERATION_RUNNING 1u
#define F4FORGE_ASYNC_OPERATION_COMPLETED 2u
#define F4FORGE_ASYNC_OPERATION_CANCELLED 3u
#define F4FORGE_WAIT_INFINITE UINT32_MAX

typedef struct F4ForgeStringView {
    const char* data;
    uint32_t length;
} F4ForgeStringView;

typedef struct F4ForgeByteView {
    const uint8_t* data;
    uint32_t length;
} F4ForgeByteView;

typedef enum F4ForgeInputDevice {
    F4FORGE_INPUT_KEYBOARD = 0,
    F4FORGE_INPUT_MOUSE = 1,
    F4FORGE_INPUT_GAMEPAD = 2
} F4ForgeInputDevice;

typedef enum F4ForgeKey {
    F4FORGE_KEY_UNKNOWN = 0,
    F4FORGE_KEY_BACKSPACE = 0x08,
    F4FORGE_KEY_TAB = 0x09,
    F4FORGE_KEY_ENTER = 0x0D,
    F4FORGE_KEY_PAUSE = 0x13,
    F4FORGE_KEY_CAPS_LOCK = 0x14,
    F4FORGE_KEY_ESCAPE = 0x1B,
    F4FORGE_KEY_SPACE = 0x20,
    F4FORGE_KEY_PAGE_UP = 0x21,
    F4FORGE_KEY_PAGE_DOWN = 0x22,
    F4FORGE_KEY_END = 0x23,
    F4FORGE_KEY_HOME = 0x24,
    F4FORGE_KEY_LEFT = 0x25,
    F4FORGE_KEY_UP = 0x26,
    F4FORGE_KEY_RIGHT = 0x27,
    F4FORGE_KEY_DOWN = 0x28,
    F4FORGE_KEY_PRINT_SCREEN = 0x2C,
    F4FORGE_KEY_INSERT = 0x2D,
    F4FORGE_KEY_DELETE = 0x2E,
    F4FORGE_KEY_0 = 0x30,
    F4FORGE_KEY_1 = 0x31,
    F4FORGE_KEY_2 = 0x32,
    F4FORGE_KEY_3 = 0x33,
    F4FORGE_KEY_4 = 0x34,
    F4FORGE_KEY_5 = 0x35,
    F4FORGE_KEY_6 = 0x36,
    F4FORGE_KEY_7 = 0x37,
    F4FORGE_KEY_8 = 0x38,
    F4FORGE_KEY_9 = 0x39,
    F4FORGE_KEY_A = 0x41,
    F4FORGE_KEY_B = 0x42,
    F4FORGE_KEY_C = 0x43,
    F4FORGE_KEY_D = 0x44,
    F4FORGE_KEY_E = 0x45,
    F4FORGE_KEY_F = 0x46,
    F4FORGE_KEY_G = 0x47,
    F4FORGE_KEY_H = 0x48,
    F4FORGE_KEY_I = 0x49,
    F4FORGE_KEY_J = 0x4A,
    F4FORGE_KEY_K = 0x4B,
    F4FORGE_KEY_L = 0x4C,
    F4FORGE_KEY_M = 0x4D,
    F4FORGE_KEY_N = 0x4E,
    F4FORGE_KEY_O = 0x4F,
    F4FORGE_KEY_P = 0x50,
    F4FORGE_KEY_Q = 0x51,
    F4FORGE_KEY_R = 0x52,
    F4FORGE_KEY_S = 0x53,
    F4FORGE_KEY_T = 0x54,
    F4FORGE_KEY_U = 0x55,
    F4FORGE_KEY_V = 0x56,
    F4FORGE_KEY_W = 0x57,
    F4FORGE_KEY_X = 0x58,
    F4FORGE_KEY_Y = 0x59,
    F4FORGE_KEY_Z = 0x5A,
    F4FORGE_KEY_APPS = 0x5D,
    F4FORGE_KEY_NUMPAD_0 = 0x60,
    F4FORGE_KEY_NUMPAD_1 = 0x61,
    F4FORGE_KEY_NUMPAD_2 = 0x62,
    F4FORGE_KEY_NUMPAD_3 = 0x63,
    F4FORGE_KEY_NUMPAD_4 = 0x64,
    F4FORGE_KEY_NUMPAD_5 = 0x65,
    F4FORGE_KEY_NUMPAD_6 = 0x66,
    F4FORGE_KEY_NUMPAD_7 = 0x67,
    F4FORGE_KEY_NUMPAD_8 = 0x68,
    F4FORGE_KEY_NUMPAD_9 = 0x69,
    F4FORGE_KEY_NUMPAD_MULTIPLY = 0x6A,
    F4FORGE_KEY_NUMPAD_PLUS = 0x6B,
    F4FORGE_KEY_NUMPAD_MINUS = 0x6D,
    F4FORGE_KEY_NUMPAD_PERIOD = 0x6E,
    F4FORGE_KEY_NUMPAD_DIVIDE = 0x6F,
    F4FORGE_KEY_F1 = 0x70,
    F4FORGE_KEY_F2 = 0x71,
    F4FORGE_KEY_F3 = 0x72,
    F4FORGE_KEY_F4 = 0x73,
    F4FORGE_KEY_F5 = 0x74,
    F4FORGE_KEY_F6 = 0x75,
    F4FORGE_KEY_F7 = 0x76,
    F4FORGE_KEY_F8 = 0x77,
    F4FORGE_KEY_F9 = 0x78,
    F4FORGE_KEY_F10 = 0x79,
    F4FORGE_KEY_F11 = 0x7A,
    F4FORGE_KEY_F12 = 0x7B,
    F4FORGE_KEY_NUM_LOCK = 0x90,
    F4FORGE_KEY_SCROLL_LOCK = 0x91,
    F4FORGE_KEY_LSHIFT = 0xA0,
    F4FORGE_KEY_RSHIFT = 0xA1,
    F4FORGE_KEY_LCONTROL = 0xA2,
    F4FORGE_KEY_RCONTROL = 0xA3,
    F4FORGE_KEY_LALT = 0xA4,
    F4FORGE_KEY_RALT = 0xA5,
    F4FORGE_KEY_SEMICOLON = 0xBA,
    F4FORGE_KEY_EQUALS = 0xBB,
    F4FORGE_KEY_COMMA = 0xBC,
    F4FORGE_KEY_MINUS = 0xBD,
    F4FORGE_KEY_PERIOD = 0xBE,
    F4FORGE_KEY_SLASH = 0xBF,
    F4FORGE_KEY_LBRACKET = 0xDB,
    F4FORGE_KEY_BACKSLASH = 0xDC,
    F4FORGE_KEY_RBRACKET = 0xDD,
    F4FORGE_KEY_APOSTROPHE = 0xDE
} F4ForgeKey;

typedef struct F4ForgeKeyEventData {
    uint32_t structSize;
    uint32_t deviceType;
    uint32_t keyCode;
    uint32_t isDown;
    uint32_t isRepeat;
    float heldSeconds;
    uint32_t isMenu;
} F4ForgeKeyEventData;

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
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t structSize;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t kind;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t version;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t flags;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t threadPolicy;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t requestSize;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t responseSize;
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t payloadSize;
    // cppcheck-suppress uninitMemberVarNoCtor
    F4ForgeStringView name;
    // cppcheck-suppress uninitMemberVarNoCtor
    F4ForgeEndpointThunk thunk;
    // cppcheck-suppress uninitMemberVarNoCtor
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
    F4ForgeModuleHandle subscriber,
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context) F4FORGE_NOEXCEPT;

typedef void (F4FORGE_CALL* F4ForgeUnsubscribeFn)(
    F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT;

typedef F4ForgeInterceptorSubscriptionHandle (F4FORGE_CALL* F4ForgeInterceptFn)(
    F4ForgeModuleHandle interceptorOwner,
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

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeInvokeAsyncFn)(
    F4ForgeModuleHandle caller,
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeEmitAsyncFn)(
    F4ForgeModuleHandle caller,
    F4ForgeEndpointHandle endpoint,
    const void* payload,
    uint32_t payloadSize,
    F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgePollOperationFn)(
    F4ForgeAsyncOperationHandle operation,
    F4ForgeAsyncOperationState* state) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeWaitOperationFn)(
    F4ForgeAsyncOperationHandle operation,
    uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeGetOperationResultFn)(
    F4ForgeAsyncOperationHandle operation,
    F4ForgeResult* invocationResult,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeCancelOperationFn)(
    F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeReleaseOperationFn)(
    F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;

typedef F4ForgeResult (F4FORGE_CALL* F4ForgeWaitModuleQuiescenceFn)(
    F4ForgeModuleHandle module,
    uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;

typedef struct F4ForgeHostApi {
    // cppcheck-suppress uninitMemberVarNoCtor
    uint32_t abiVersion;
    // cppcheck-suppress uninitMemberVarNoCtor
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
    F4ForgeInvokeAsyncFn invokeAsync;
    F4ForgeEmitAsyncFn emitAsync;
    F4ForgePollOperationFn pollOperation;
    F4ForgeWaitOperationFn waitOperation;
    F4ForgeGetOperationResultFn getOperationResult;
    F4ForgeCancelOperationFn cancelOperation;
    F4ForgeReleaseOperationFn releaseOperation;
    F4ForgeWaitModuleQuiescenceFn waitModuleQuiescence;
} F4ForgeHostApi;

typedef const F4ForgeHostApi* (F4FORGE_CALL* F4ForgeGetHostApiFn)(void) F4FORGE_NOEXCEPT;

#if defined(_MSC_VER)
__declspec(dllexport)
#endif
const F4ForgeHostApi* F4FORGE_CALL F4ForgeGetHostApi(void) F4FORGE_NOEXCEPT;

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
static_assert(sizeof(F4ForgeKeyEventData) == 28);
static_assert(offsetof(F4ForgeKeyEventData, structSize) == 0);
static_assert(offsetof(F4ForgeKeyEventData, deviceType) == 4);
static_assert(offsetof(F4ForgeKeyEventData, keyCode) == 8);
static_assert(offsetof(F4ForgeKeyEventData, isDown) == 12);
static_assert(offsetof(F4ForgeKeyEventData, isRepeat) == 16);
static_assert(offsetof(F4ForgeKeyEventData, heldSeconds) == 20);
static_assert(offsetof(F4ForgeKeyEventData, isMenu) == 24);
static_assert(sizeof(F4ForgeResult) == 4);
static_assert(sizeof(F4ForgeAsyncOperationState) == 4);
static_assert(sizeof(F4ForgeRawHandle) == 8);
static_assert(sizeof(F4ForgeEndpointHandle) == 8);
static_assert(sizeof(F4ForgeEventSubscriptionHandle) == 8);
static_assert(sizeof(F4ForgeInterceptorSubscriptionHandle) == 8);
static_assert(sizeof(F4ForgeModuleHandle) == 8);
static_assert(sizeof(F4ForgeRuntimeHandle) == 8);
static_assert(sizeof(F4ForgePluginHandle) == 8);
static_assert(sizeof(F4ForgeAsyncOperationHandle) == 8);
static_assert(offsetof(F4ForgeStringView, data) == 0);
static_assert(offsetof(F4ForgeStringView, length) == 8);
static_assert(offsetof(F4ForgeByteView, data) == 0);
static_assert(offsetof(F4ForgeByteView, length) == 8);
static_assert(offsetof(F4ForgeHostApi, abiVersion) == 0);
static_assert(offsetof(F4ForgeHostApi, structSize) == 4);
static_assert(offsetof(F4ForgeHostApi, resolveEndpoint) == 8);
static_assert(offsetof(F4ForgeHostApi, invoke) == 16);
static_assert(offsetof(F4ForgeHostApi, subscribe) == 24);
static_assert(offsetof(F4ForgeHostApi, unsubscribe) == 32);
static_assert(offsetof(F4ForgeHostApi, intercept) == 40);
static_assert(offsetof(F4ForgeHostApi, removeInterceptor) == 48);
static_assert(offsetof(F4ForgeHostApi, registerEndpoint) == 56);
static_assert(offsetof(F4ForgeHostApi, registerModule) == 64);
static_assert(offsetof(F4ForgeHostApi, unregisterModule) == 72);
static_assert(offsetof(F4ForgeHostApi, queryCapability) == 80);
static_assert(offsetof(F4ForgeHostApi, queueTask) == 88);
static_assert(offsetof(F4ForgeHostApi, log) == 96);
static_assert(offsetof(F4ForgeHostApi, invokeAsync) == 104);
static_assert(offsetof(F4ForgeHostApi, emitAsync) == 112);
static_assert(offsetof(F4ForgeHostApi, pollOperation) == 120);
static_assert(offsetof(F4ForgeHostApi, waitOperation) == 128);
static_assert(offsetof(F4ForgeHostApi, getOperationResult) == 136);
static_assert(offsetof(F4ForgeHostApi, cancelOperation) == 144);
static_assert(offsetof(F4ForgeHostApi, releaseOperation) == 152);
static_assert(offsetof(F4ForgeHostApi, waitModuleQuiescence) == 160);
static_assert(sizeof(F4ForgeHostApi) == 168);
static_assert(offsetof(F4ForgeEndpointDefinition, structSize) == 0);
static_assert(offsetof(F4ForgeEndpointDefinition, kind) == 4);
static_assert(offsetof(F4ForgeEndpointDefinition, version) == 8);
static_assert(offsetof(F4ForgeEndpointDefinition, flags) == 12);
static_assert(offsetof(F4ForgeEndpointDefinition, threadPolicy) == 16);
static_assert(offsetof(F4ForgeEndpointDefinition, requestSize) == 20);
static_assert(offsetof(F4ForgeEndpointDefinition, responseSize) == 24);
static_assert(offsetof(F4ForgeEndpointDefinition, payloadSize) == 28);
static_assert(offsetof(F4ForgeEndpointDefinition, name) == 32);
static_assert(offsetof(F4ForgeEndpointDefinition, thunk) == 48);
static_assert(offsetof(F4ForgeEndpointDefinition, context) == 56);
static_assert(sizeof(F4ForgeEndpointDefinition) == 64);
#endif
