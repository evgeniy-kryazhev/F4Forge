#include "f4forge_host.h"

#include <cstring>
#include <vector>

namespace f4forge::core {

F4ForgeHost::F4ForgeHost() noexcept
    : _events(_endpoints),
      _interceptors(_endpoints),
      _runtimes(),
      _operations(),
      _modules(_endpoints, _events, _interceptors, _capabilities, &_runtimes, &_operations)
{
    _runtimes.SetModuleShutdownCallback([this](F4ForgeRuntimeHandle runtime) {
        return _modules.UnregisterRuntime(runtime);
    });
    _api.abiVersion = F4FORGE_ABI_VERSION;
    _api.structSize = sizeof(F4ForgeHostApi);
    _api.resolveEndpoint = &ResolveEndpoint;
    _api.invoke = &Invoke;
    _api.subscribe = &Subscribe;
    _api.unsubscribe = &Unsubscribe;
    _api.intercept = &Intercept;
    _api.removeInterceptor = &RemoveInterceptor;
    _api.registerEndpoint = &RegisterEndpoint;
    _api.registerModule = &RegisterModule;
    _api.unregisterModule = &UnregisterModule;
    _api.queryCapability = &QueryCapability;
    _api.queueTask = &QueueTask;
    _api.log = &Log;
    _api.invokeAsync = &InvokeAsync;
    _api.emitAsync = &EmitAsync;
    _api.pollOperation = &PollOperation;
    _api.waitOperation = &WaitOperation;
    _api.getOperationResult = &GetOperationResult;
    _api.cancelOperation = &CancelOperation;
    _api.releaseOperation = &ReleaseOperation;
    _api.waitModuleQuiescence = &WaitModuleQuiescence;

    static EndpointOwner coreOwner;
    static constexpr F4ForgeStringView coreCapabilities[] = {
        { "core", sizeof("core") - 1 },
        { "core.registry", sizeof("core.registry") - 1 },
        { "core.runtime", sizeof("core.runtime") - 1 },
        { "core.thread", sizeof("core.thread") - 1 },
        { "core.lifecycle", sizeof("core.lifecycle") - 1 }
    };
    for (const auto capability : coreCapabilities)
        _capabilities.Register(capability, 1, &coreOwner);
}

const F4ForgeHostApi& F4ForgeHost::Api() const noexcept
{
    return _api;
}

F4ForgeHostBinding F4ForgeHost::Binding() noexcept
{
    return { F4FORGE_ABI_VERSION, sizeof(F4ForgeHostBinding), &_api, this };
}

EndpointRegistry& F4ForgeHost::Endpoints() noexcept
{
    return _endpoints;
}

EventRegistry& F4ForgeHost::Events() noexcept
{
    return _events;
}

InterceptorRegistry& F4ForgeHost::Interceptors() noexcept
{
    return _interceptors;
}

CapabilityRegistry& F4ForgeHost::Capabilities() noexcept
{
    return _capabilities;
}

ModuleManager& F4ForgeHost::Modules() noexcept
{
    return _modules;
}

RuntimeManager& F4ForgeHost::Runtimes() noexcept
{
    return _runtimes;
}

F4ForgeResult F4ForgeHost::Shutdown() noexcept
{
    _operations.Shutdown();
    return _runtimes.ShutdownAll();
}

void F4ForgeHost::SetGameThreadScheduler(GameThreadScheduler* scheduler) noexcept
{
    _gameThreadScheduler = scheduler;
    _operations.SetScheduler(scheduler);
}

AsyncOperationRegistry& F4ForgeHost::Operations() noexcept
{
    return _operations;
}

F4ForgeEndpointHandle F4FORGE_CALL F4ForgeHost::ResolveEndpoint(
    void* hostContext,
    F4ForgeStringView name,
    uint32_t version) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_INVALID_HANDLE;
        return static_cast<F4ForgeHost*>(hostContext)->_endpoints.Resolve(name, version);
    } catch (...) {
        return F4FORGE_INVALID_HANDLE;
    }
}

// cppcheck-suppress constParameterCallback
F4ForgeResult F4FORGE_CALL F4ForgeHost::Invoke(
    void* hostContext,
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    // cppcheck-suppress constParameterCallback
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        return static_cast<F4ForgeHost*>(hostContext)->_endpoints.Invoke(endpoint, request, requestSize, response, responseCapacity, responseSize);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::RegisterEndpoint(
    void* hostContext,
    F4ForgeModuleHandle module,
    const F4ForgeEndpointDefinition* definition,
    F4ForgeEndpointHandle* endpoint) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr || definition == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try {
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        return host._endpoints.Register(*definition, host._modules.Owner(module), endpoint);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::RegisterModule(
    void* hostContext,
    F4ForgeRuntimeHandle runtime,
    F4ForgeStringView id,
    uint32_t version,
    F4ForgeModuleHandle* module) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        return static_cast<F4ForgeHost*>(hostContext)->_modules.Register(runtime, id, version, module);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::UnregisterModule(void* hostContext, F4ForgeModuleHandle module) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        return host._modules.Unregister(module);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

uint32_t F4FORGE_CALL F4ForgeHost::QueryCapability(
    void* hostContext,
    F4ForgeStringView id,
    uint32_t minimumVersion) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return 0;
        return static_cast<F4ForgeHost*>(hostContext)->_capabilities.Query(id, minimumVersion);
    } catch (...) {
        return 0;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::QueueTask(
    void* hostContext,
    F4ForgeRuntimeHandle runtime,
    uint64_t taskHandle) F4FORGE_NOEXCEPT
{
    struct Task final {
        RuntimeManager* runtimes;
        F4ForgeRuntimeHandle runtime;
        uint64_t taskHandle;
    };
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        if (!host._runtimes.BeginTaskAdmission(runtime)) return F4FORGE_RESULT_INACTIVE_RUNTIME;
        struct Admission final {
            RuntimeManager& runtimes;
            F4ForgeRuntimeHandle runtime;
            ~Admission() { runtimes.EndTaskAdmission(runtime); }
        } admission{ host._runtimes, runtime };
        if (host._gameThreadScheduler == nullptr) return F4FORGE_RESULT_SCHEDULER_UNAVAILABLE;
        auto* task = new Task{ &host._runtimes, runtime, taskHandle };
        const auto result = host._gameThreadScheduler->Post(
            [](void* context) noexcept {
                const auto* item = static_cast<Task*>(context);
                item->runtimes->ExecuteTask(item->runtime, item->taskHandle);
            },
            task,
            [](void* context) noexcept { delete static_cast<Task*>(context); });
        if (result != F4FORGE_RESULT_SUCCESS) delete task;
        return result;
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::InvokeAsync(
    void* hostContext,
    F4ForgeModuleHandle caller,
    F4ForgeEndpointHandle endpoint,
    const void* request,
    uint32_t requestSize,
    F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        auto* requester = host._modules.Owner(caller);
        if (requester == nullptr) return F4FORGE_RESULT_INACTIVE_MODULE;
        auto requesterLease = requester->TryAcquireDispatchLease();
        if (!requesterLease) return F4FORGE_RESULT_INACTIVE_MODULE;
        if (host._endpoints.Kind(endpoint) != F4FORGE_ENDPOINT_METHOD)
            return F4FORGE_RESULT_INVALID_HANDLE;
        if (requestSize != 0 && request == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        std::vector<uint8_t> requestCopy(requestSize);
        if (requestSize != 0) std::memcpy(requestCopy.data(), request, requestSize);
        const auto responseCapacity = host._endpoints.ResponseSize(endpoint);
        if (responseCapacity == UINT32_MAX) return F4FORGE_RESULT_INVALID_ARGUMENT;
        const auto requiresGameThread = host._endpoints.IsGameOnly(endpoint);
        return host._operations.Create(
            caller,
            requiresGameThread,
            responseCapacity,
            [&host, endpoint, request = std::move(requestCopy), requestSize](AsyncOperation& async) {
                uint32_t responseSize = 0;
                const auto result = host._endpoints.Invoke(
                    endpoint,
                    requestSize == 0 ? nullptr : request.data(),
                    requestSize,
                    async.ResponseData(),
                    async.ResponseCapacity(),
                    &responseSize);
                async.SetResponseSize(responseSize);
                return result;
            },
            operation);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::EmitAsync(
    void* hostContext,
    F4ForgeModuleHandle caller,
    F4ForgeEndpointHandle endpoint,
    const void* payload,
    uint32_t payloadSize,
    F4ForgeAsyncOperationHandle* operation) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        auto* requester = host._modules.Owner(caller);
        if (requester == nullptr) return F4FORGE_RESULT_INACTIVE_MODULE;
        auto requesterLease = requester->TryAcquireDispatchLease();
        if (!requesterLease) return F4FORGE_RESULT_INACTIVE_MODULE;
        if (host._endpoints.Kind(endpoint) != F4FORGE_ENDPOINT_EVENT)
            return F4FORGE_RESULT_INVALID_HANDLE;
        if (payloadSize != 0 && payload == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        std::vector<uint8_t> payloadCopy(payloadSize);
        if (payloadSize != 0) std::memcpy(payloadCopy.data(), payload, payloadSize);
        const auto requiresGameThread = host._endpoints.IsGameOnly(endpoint);
        return host._operations.Create(
            caller,
            requiresGameThread,
            0,
            [&host, endpoint, payload = std::move(payloadCopy), payloadSize](AsyncOperation&) {
                return host._events.Emit(
                    endpoint,
                    payloadSize == 0 ? nullptr : payload.data(),
                    payloadSize);
            },
            operation);
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::PollOperation(
    void* hostContext,
    F4ForgeAsyncOperationHandle operation,
    F4ForgeAsyncOperationState* state) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try { return static_cast<F4ForgeHost*>(hostContext)->_operations.Poll(operation, state); }
    catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::WaitOperation(
    void* hostContext,
    F4ForgeAsyncOperationHandle operation,
    uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try { return static_cast<F4ForgeHost*>(hostContext)->_operations.Wait(operation, timeoutMilliseconds); }
    catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::GetOperationResult(
    void* hostContext,
    F4ForgeAsyncOperationHandle operation,
    F4ForgeResult* invocationResult,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
        return static_cast<F4ForgeHost*>(hostContext)->_operations.GetResult(
            operation, invocationResult, response, responseCapacity, responseSize);
    } catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::CancelOperation(void* hostContext, F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try { return static_cast<F4ForgeHost*>(hostContext)->_operations.Cancel(operation); }
    catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::ReleaseOperation(void* hostContext, F4ForgeAsyncOperationHandle operation) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try { return static_cast<F4ForgeHost*>(hostContext)->_operations.Release(operation); }
    catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

F4ForgeResult F4FORGE_CALL F4ForgeHost::WaitModuleQuiescence(
    void* hostContext,
    F4ForgeModuleHandle module, uint32_t timeoutMilliseconds) F4FORGE_NOEXCEPT
{
    if (hostContext == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    try { return static_cast<F4ForgeHost*>(hostContext)->_modules.WaitForQuiescence(module, timeoutMilliseconds); }
    catch (...) { return F4FORGE_RESULT_INTERNAL_ERROR; }
}

void F4ForgeHost::SetLogSink(LogSink sink) noexcept
{
    _logSink.store(sink, std::memory_order_release);
}

void F4FORGE_CALL F4ForgeHost::Log(void* hostContext, uint32_t level, F4ForgeStringView message) F4FORGE_NOEXCEPT
{
    if (message.data == nullptr && message.length != 0) return;
    if (hostContext == nullptr) return;
    const auto sink = static_cast<F4ForgeHost*>(hostContext)->_logSink.load(std::memory_order_acquire);
    if (sink == nullptr) return;
    try {
        sink(level, std::string_view(message.data == nullptr ? "" : message.data, message.length));
    } catch (...) {
    }
}

F4ForgeEventSubscriptionHandle F4FORGE_CALL F4ForgeHost::Subscribe(
    void* hostContext,
    F4ForgeModuleHandle subscriber,
    F4ForgeEndpointHandle endpoint,
    F4ForgeEventCallback callback,
    void* context) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_INVALID_HANDLE;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        return host._events.Subscribe(host._modules.Owner(subscriber), endpoint, callback, context);
    } catch (...) {
        return F4FORGE_INVALID_HANDLE;
    }
}

void F4FORGE_CALL F4ForgeHost::Unsubscribe(void* hostContext, F4ForgeEventSubscriptionHandle subscription) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return;
        static_cast<F4ForgeHost*>(hostContext)->_events.Unsubscribe(subscription);
    } catch (...) {
    }
}

F4ForgeInterceptorSubscriptionHandle F4FORGE_CALL F4ForgeHost::Intercept(
    void* hostContext,
    F4ForgeModuleHandle interceptorOwner,
    F4ForgeEndpointHandle endpoint,
    F4ForgeInterceptorCallback callback,
    void* context) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return F4FORGE_INVALID_HANDLE;
        auto& host = *static_cast<F4ForgeHost*>(hostContext);
        return host._interceptors.Intercept(host._modules.Owner(interceptorOwner), endpoint, callback, context);
    } catch (...) {
        return F4FORGE_INVALID_HANDLE;
    }
}

void F4FORGE_CALL F4ForgeHost::RemoveInterceptor(void* hostContext, F4ForgeInterceptorSubscriptionHandle subscription) F4FORGE_NOEXCEPT
{
    try {
        if (hostContext == nullptr) return;
        static_cast<F4ForgeHost*>(hostContext)->_interceptors.Remove(subscription);
    } catch (...) {
    }
}

}
