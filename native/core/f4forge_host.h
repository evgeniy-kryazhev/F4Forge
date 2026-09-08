#pragma once

#include "async/async_operation.h"
#include "f4forge_abi.h"
#include "modules/module_manager.h"
#include "registry/event_registry.h"
#include "registry/capability_registry.h"
#include "registry/interceptor_registry.h"
#include "runtime/runtime_manager.h"

#include <atomic>
#include <string_view>

namespace f4forge::core {

class F4ForgeHost final {
public:
    static F4ForgeHost& Instance() noexcept;

    const F4ForgeHostApi& Api() const noexcept;
    EndpointRegistry& Endpoints() noexcept;
    EventRegistry& Events() noexcept;
    InterceptorRegistry& Interceptors() noexcept;
    CapabilityRegistry& Capabilities() noexcept;
    ModuleManager& Modules() noexcept;
    RuntimeManager& Runtimes() noexcept;
    F4ForgeResult Shutdown() noexcept;
    void SetGameThreadScheduler(GameThreadScheduler* scheduler) noexcept;
    AsyncOperationRegistry& Operations() noexcept;
    using LogSink = void (*)(uint32_t level, std::string_view message) noexcept;
    void SetLogSink(LogSink sink) noexcept;

private:
    F4ForgeHost() noexcept;

    static F4ForgeEndpointHandle F4FORGE_CALL ResolveEndpoint(
        F4ForgeStringView name,
        uint32_t version) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL Invoke(
        F4ForgeEndpointHandle endpoint,
        const void* request,
        uint32_t requestSize,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL RegisterEndpoint(
        F4ForgeModuleHandle module,
        const F4ForgeEndpointDefinition* definition,
        F4ForgeEndpointHandle* endpoint) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL RegisterModule(
        F4ForgeRuntimeHandle runtime,
        F4ForgeStringView id,
        uint32_t version,
        F4ForgeModuleHandle* module) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL UnregisterModule(
        F4ForgeModuleHandle module) F4FORGE_NOEXCEPT;
    static uint32_t F4FORGE_CALL QueryCapability(
        F4ForgeStringView id,
        uint32_t minimumVersion) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL QueueTask(
        F4ForgeRuntimeHandle runtime,
        uint64_t taskHandle,
        void* context) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL InvokeAsync(
        F4ForgeModuleHandle caller,
        F4ForgeEndpointHandle endpoint,
        const void* request,
        uint32_t requestSize,
        F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL EmitAsync(
        F4ForgeModuleHandle caller,
        F4ForgeEndpointHandle endpoint,
        const void* payload,
        uint32_t payloadSize,
        F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL PollOperation(
        F4ForgeAsyncOperationHandle operation,
        F4ForgeAsyncOperationState* state) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL WaitOperation(
        F4ForgeAsyncOperationHandle operation,
        uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL GetOperationResult(
        F4ForgeAsyncOperationHandle operation,
        F4ForgeResult* invocationResult,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL CancelOperation(
        F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL ReleaseOperation(
        F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL WaitModuleQuiescence(
        F4ForgeModuleHandle module, uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL Log(
        uint32_t level,
        F4ForgeStringView message) F4FORGE_NOEXCEPT;
    static F4ForgeEventSubscriptionHandle F4FORGE_CALL Subscribe(
        F4ForgeModuleHandle subscriber,
        F4ForgeEndpointHandle endpoint,
        F4ForgeEventCallback callback,
        void* context) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL Unsubscribe(
        F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT;
    static F4ForgeInterceptorSubscriptionHandle F4FORGE_CALL Intercept(
        F4ForgeModuleHandle interceptorOwner,
        F4ForgeEndpointHandle endpoint,
        F4ForgeInterceptorCallback callback,
        void* context) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL RemoveInterceptor(
        F4ForgeInterceptorSubscriptionHandle subscription) F4FORGE_NOEXCEPT;

    EndpointRegistry _endpoints;
    EventRegistry _events;
    InterceptorRegistry _interceptors;
    CapabilityRegistry _capabilities;
    RuntimeManager _runtimes;
    AsyncOperationRegistry _operations;
    ModuleManager _modules;
    F4ForgeHostApi _api{};
    std::atomic<LogSink> _logSink{ nullptr };
};

}
