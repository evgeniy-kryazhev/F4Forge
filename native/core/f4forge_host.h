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
    F4ForgeHost() noexcept;

    const F4ForgeHostApi& Api() const noexcept;
    F4ForgeHostBinding Binding() noexcept;
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
    static F4ForgeEndpointHandle F4FORGE_CALL ResolveEndpoint(
        void* hostContext,
        F4ForgeStringView name,
        uint32_t version) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL Invoke(
        void* hostContext,
        F4ForgeEndpointHandle endpoint,
        const void* request,
        uint32_t requestSize,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL RegisterEndpoint(
        void* hostContext,
        F4ForgeModuleHandle module,
        const F4ForgeEndpointDefinition* definition,
        F4ForgeEndpointHandle* endpoint) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL RegisterModule(
        void* hostContext,
        F4ForgeRuntimeHandle runtime,
        F4ForgeStringView id,
        uint32_t version,
        F4ForgeModuleHandle* module) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL UnregisterModule(
        void* hostContext,
        F4ForgeModuleHandle module) F4FORGE_NOEXCEPT;
    static uint32_t F4FORGE_CALL QueryCapability(
        void* hostContext,
        F4ForgeStringView id,
        uint32_t minimumVersion) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL QueueTask(
        void* hostContext,
        F4ForgeRuntimeHandle runtime,
        uint64_t taskHandle) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL InvokeAsync(
        void* hostContext,
        F4ForgeModuleHandle caller,
        F4ForgeEndpointHandle endpoint,
        const void* request,
        uint32_t requestSize,
        F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL EmitAsync(
        void* hostContext,
        F4ForgeModuleHandle caller,
        F4ForgeEndpointHandle endpoint,
        const void* payload,
        uint32_t payloadSize,
        F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL PollOperation(
        void* hostContext,
        F4ForgeAsyncOperationHandle operation,
        F4ForgeAsyncOperationState* state) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL WaitOperation(
        void* hostContext,
        F4ForgeAsyncOperationHandle operation,
        uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL GetOperationResult(
        void* hostContext,
        F4ForgeAsyncOperationHandle operation,
        F4ForgeResult* invocationResult,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL CancelOperation(
        void* hostContext,
        F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL ReleaseOperation(
        void* hostContext,
        F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL WaitModuleQuiescence(
        void* hostContext,
        F4ForgeModuleHandle module, uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL Log(
        void* hostContext,
        uint32_t level,
        F4ForgeStringView message) F4FORGE_NOEXCEPT;
    static F4ForgeEventSubscriptionHandle F4FORGE_CALL Subscribe(
        void* hostContext,
        F4ForgeModuleHandle subscriber,
        F4ForgeEndpointHandle endpoint,
        F4ForgeEventCallback callback,
        void* context) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL Unsubscribe(
        void* hostContext,
        F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT;
    static F4ForgeInterceptorSubscriptionHandle F4FORGE_CALL Intercept(
        void* hostContext,
        F4ForgeModuleHandle interceptorOwner,
        F4ForgeEndpointHandle endpoint,
        F4ForgeInterceptorCallback callback,
        void* context) F4FORGE_NOEXCEPT;
    static void F4FORGE_CALL RemoveInterceptor(
        void* hostContext,
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
    GameThreadScheduler* _gameThreadScheduler{};
};

}
