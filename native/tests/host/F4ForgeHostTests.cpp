#include "f4forge_host.h"

#include <cassert>
#include <condition_variable>
#include <mutex>
#include <thread>
#include <vector>

namespace {

std::thread::id gameThread;
uint32_t gameOnlyCalls = 0;
uint32_t eventCalls = 0;
F4ForgeModuleHandle providerModule = F4FORGE_INVALID_HANDLE;

int32_t F4FORGE_CALL IsGameThread() noexcept
{
    return std::this_thread::get_id() == gameThread ? 1 : 0;
}

F4ForgeResult F4FORGE_CALL Success(
    void*, const void*, uint32_t, void*, uint32_t, uint32_t*) noexcept
{
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult F4FORGE_CALL GameOnly(
    void*, const void*, uint32_t, void* response, uint32_t responseCapacity, uint32_t* responseSize) noexcept
{
    ++gameOnlyCalls;
    if (responseCapacity != sizeof(uint32_t) || response == nullptr) return F4FORGE_RESULT_INVALID_RESPONSE_CAPACITY;
    *static_cast<uint32_t*>(response) = 42;
    if (responseSize != nullptr) *responseSize = sizeof(uint32_t);
    return F4FORGE_RESULT_SUCCESS;
}

void F4FORGE_CALL OnEvent(
    F4ForgeEventSubscriptionHandle,
    F4ForgeEndpointHandle,
    const void*,
    uint32_t,
    void*) noexcept
{
    ++eventCalls;
}

F4ForgeResult F4FORGE_CALL InitializeRuntime(const F4ForgeRuntimeInitializeParams* params) noexcept
{
    if (params != nullptr && params->host != nullptr && params->host->registerModule != nullptr) {
        const auto result = params->host->registerModule(
            params->runtime, { "host.provider-module", sizeof("host.provider-module") - 1 }, 1,
            &providerModule);
        if (result != F4FORGE_RESULT_SUCCESS) return result;
    }
    return F4FORGE_RESULT_SUCCESS;
}

void F4FORGE_CALL ShutdownRuntime(F4ForgeRuntimeHandle) noexcept {}
void F4FORGE_CALL ExecuteRuntimeTask(const F4ForgeRuntimeTask*) noexcept {}

class TestScheduler final : public f4forge::core::GameThreadScheduler {
public:
    bool IsGameThread() const noexcept override
    {
        return std::this_thread::get_id() == gameThread;
    }

    F4ForgeResult Post(
        f4forge::core::SchedulerJob job,
        void* context,
        f4forge::core::SchedulerCleanup cleanup) noexcept override
    {
        std::lock_guard lock(_mutex);
        _jobs.push_back({ job, context, cleanup });
        return F4FORGE_RESULT_SUCCESS;
    }

    void CancelPending() noexcept override
    {
        std::vector<Job> jobs;
        {
            std::lock_guard lock(_mutex);
            jobs.swap(_jobs);
        }
        for (const auto& job : jobs) job.cleanup(job.context);
    }

    bool RunOne()
    {
        Job job;
        {
            std::lock_guard lock(_mutex);
            if (_jobs.empty()) return false;
            job = _jobs.front();
            _jobs.erase(_jobs.begin());
        }
        job.job(job.context);
        job.cleanup(job.context);
        return true;
    }

private:
    struct Job final {
        f4forge::core::SchedulerJob job;
        void* context{};
        f4forge::core::SchedulerCleanup cleanup{};
    };

    mutable std::mutex _mutex;
    std::vector<Job> _jobs;
};

}

int main()
{
    gameThread = std::this_thread::get_id();
    TestScheduler scheduler;
    const auto& api = f4forge::core::F4ForgeHost::Instance().Api();
    auto& host = f4forge::core::F4ForgeHost::Instance();
    host.SetGameThreadScheduler(&scheduler);
    host.Endpoints().SetGameThreadCheck(&IsGameThread);
    assert(api.abiVersion == F4FORGE_ABI_VERSION);
    assert(api.structSize == sizeof(F4ForgeHostApi));
    assert(api.resolveEndpoint != nullptr);
    assert(api.invoke != nullptr);
    assert(api.registerEndpoint != nullptr);
    assert(api.invokeAsync != nullptr);
    assert(api.emitAsync != nullptr);
    assert(api.pollOperation != nullptr);
    assert(api.waitOperation != nullptr);
    assert(api.getOperationResult != nullptr);
    assert(api.cancelOperation != nullptr);
    assert(api.releaseOperation != nullptr);

    static const F4ForgeRuntimeInfo runtimeInfo{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeInfo),
        { "host-test-runtime", sizeof("host-test-runtime") - 1 },
        { "Host Test Runtime", sizeof("Host Test Runtime") - 1 },
        1
    };
    static const F4ForgeRuntimeProvider runtimeProvider{
        F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
        sizeof(F4ForgeRuntimeProvider),
        &runtimeInfo,
        &InitializeRuntime,
        &ShutdownRuntime,
        &ExecuteRuntimeTask
    };
    f4forge::core::RuntimeProvider provider{ runtimeInfo, runtimeProvider };
    auto& runtimes = host.Runtimes();
    const auto providerResult = runtimes.RegisterProvider(provider);
    assert(providerResult == F4FORGE_RESULT_SUCCESS);
    F4ForgeRuntimeHandle runtime = F4FORGE_INVALID_HANDLE;
    const auto runtimeInitializeResult = runtimes.Initialize(
        { "host-test-runtime", sizeof("host-test-runtime") - 1 }, &api, {}, {}, &runtime);
    assert(runtimeInitializeResult == F4FORGE_RESULT_SUCCESS);
    const auto providerModuleActive = host.Modules().IsActive(providerModule);
    assert(providerModuleActive);
    assert(api.registerModule != nullptr);
    assert(api.unregisterModule != nullptr);
    assert(api.queryCapability({ "core", sizeof("core") - 1 }, 1) == 1);
    assert(api.queryCapability({ "missing", sizeof("missing") - 1 }, 1) == 0);

    F4ForgeModuleHandle module = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(F4FORGE_INVALID_HANDLE, { "host.invalid", sizeof("host.invalid") - 1 }, 1, &module)
        == F4FORGE_RESULT_INVALID_ARGUMENT);
    assert(api.registerModule(runtime, { "host.test", sizeof("host.test") - 1 }, 1, &module)
        == F4FORGE_RESULT_SUCCESS);

    const F4ForgeEndpointDefinition definition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_METHOD,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_ANY,
        0,
        0,
        0,
        { "host.test.success", sizeof("host.test.success") - 1 },
        &Success,
        nullptr
    };
    F4ForgeEndpointHandle endpoint = F4FORGE_INVALID_HANDLE;
    assert(api.registerEndpoint(module, &definition, &endpoint) == F4FORGE_RESULT_SUCCESS);
    assert(api.resolveEndpoint({ "host.test.success", sizeof("host.test.success") - 1 }, 1) == endpoint);
    assert(api.invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_SUCCESS);
    F4ForgeAsyncOperationHandle anyOperation = F4FORGE_INVALID_HANDLE;
    assert(api.invokeAsync(module, endpoint, nullptr, 0, &anyOperation) == F4FORGE_RESULT_SUCCESS);
    F4ForgeAsyncOperationState anyState = F4FORGE_ASYNC_OPERATION_PENDING;
    assert(api.pollOperation(anyOperation, &anyState) == F4FORGE_RESULT_SUCCESS);
    assert(anyState == F4FORGE_ASYNC_OPERATION_COMPLETED);
    F4ForgeResult anyInvocationResult = F4FORGE_RESULT_INTERNAL_ERROR;
    assert(api.getOperationResult(anyOperation, &anyInvocationResult, nullptr, 0, nullptr)
        == F4FORGE_RESULT_SUCCESS);
    assert(anyInvocationResult == F4FORGE_RESULT_SUCCESS);
    assert(api.releaseOperation(anyOperation) == F4FORGE_RESULT_SUCCESS);
    assert(api.subscribe(module, endpoint, nullptr, nullptr) == F4FORGE_INVALID_HANDLE);
    assert(api.queueTask(1, 1, nullptr) == F4FORGE_RESULT_RUNTIME_UNAVAILABLE);
    assert(api.unregisterModule(module) == F4FORGE_RESULT_SUCCESS);
    assert(api.invoke(endpoint, nullptr, 0, nullptr, 0, nullptr) == F4FORGE_RESULT_STALE_HANDLE);

    F4ForgeModuleHandle gameModule = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(runtime, { "host.game", sizeof("host.game") - 1 }, 1, &gameModule)
        == F4FORGE_RESULT_SUCCESS);
    const F4ForgeEndpointDefinition gameDefinition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_METHOD,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY,
        0,
        sizeof(uint32_t),
        0,
        { "host.game.only", sizeof("host.game.only") - 1 },
        &GameOnly,
        nullptr
    };
    F4ForgeEndpointHandle gameEndpoint = F4FORGE_INVALID_HANDLE;
    assert(api.registerEndpoint(gameModule, &gameDefinition, &gameEndpoint) == F4FORGE_RESULT_SUCCESS);

    uint32_t gameResponse = 0;
    std::thread worker([&] {
        assert(api.invoke(gameEndpoint, nullptr, 0, &gameResponse, sizeof(gameResponse), nullptr)
            == F4FORGE_RESULT_WRONG_THREAD);
    });
    worker.join();
    assert(gameOnlyCalls == 0);

    F4ForgeAsyncOperationHandle operation = F4FORGE_INVALID_HANDLE;
    std::thread submitter([&] {
        assert(api.invokeAsync(gameModule, gameEndpoint, nullptr, 0, &operation)
            == F4FORGE_RESULT_SUCCESS);
    });
    submitter.join();
    F4ForgeAsyncOperationState state = F4FORGE_ASYNC_OPERATION_PENDING;
    assert(api.pollOperation(operation, &state) == F4FORGE_RESULT_SUCCESS);
    assert(state == F4FORGE_ASYNC_OPERATION_PENDING);
    assert(api.releaseOperation(operation) == F4FORGE_RESULT_OPERATION_BUSY);
    assert(api.waitOperation(operation, F4FORGE_WAIT_INFINITE) == F4FORGE_RESULT_WOULD_DEADLOCK);
    std::thread timedWaiter([&] {
        assert(api.waitOperation(operation, 0) == F4FORGE_RESULT_TIMEOUT);
    });
    timedWaiter.join();
    assert(scheduler.RunOne());
    assert(api.pollOperation(operation, &state) == F4FORGE_RESULT_SUCCESS);
    assert(state == F4FORGE_ASYNC_OPERATION_COMPLETED);
    F4ForgeResult invocationResult = F4FORGE_RESULT_INTERNAL_ERROR;
    uint32_t responseSize = 0;
    assert(api.getOperationResult(
        operation, &invocationResult, &gameResponse, sizeof(gameResponse), &responseSize)
        == F4FORGE_RESULT_SUCCESS);
    assert(invocationResult == F4FORGE_RESULT_SUCCESS);
    assert(gameResponse == 42 && responseSize == sizeof(uint32_t) && gameOnlyCalls == 1);
    assert(api.releaseOperation(operation) == F4FORGE_RESULT_SUCCESS);
    assert(api.pollOperation(operation, &state) == F4FORGE_RESULT_INVALID_HANDLE);

    F4ForgeAsyncOperationHandle cancelledOperation = F4FORGE_INVALID_HANDLE;
    std::thread cancelledSubmitter([&] {
        assert(api.invokeAsync(gameModule, gameEndpoint, nullptr, 0, &cancelledOperation)
            == F4FORGE_RESULT_SUCCESS);
    });
    cancelledSubmitter.join();
    assert(api.cancelOperation(cancelledOperation) == F4FORGE_RESULT_SUCCESS);
    assert(api.waitOperation(cancelledOperation, F4FORGE_WAIT_INFINITE) == F4FORGE_RESULT_SUCCESS);
    assert(api.releaseOperation(cancelledOperation) == F4FORGE_RESULT_SUCCESS);
    assert(scheduler.RunOne());
    assert(gameOnlyCalls == 1);

    const F4ForgeEndpointDefinition eventDefinition{
        sizeof(F4ForgeEndpointDefinition),
        F4FORGE_ENDPOINT_EVENT,
        1,
        F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY,
        0,
        0,
        sizeof(uint32_t),
        { "host.game.event", sizeof("host.game.event") - 1 },
        &Success,
        nullptr
    };
    F4ForgeEndpointHandle eventEndpoint = F4FORGE_INVALID_HANDLE;
    assert(api.registerEndpoint(gameModule, &eventDefinition, &eventEndpoint) == F4FORGE_RESULT_SUCCESS);
    const auto eventSubscription = api.subscribe(gameModule, eventEndpoint, &OnEvent, nullptr);
    assert(eventSubscription != F4FORGE_INVALID_HANDLE);
    uint32_t eventPayload = 7;
    F4ForgeResult syncEventResult = F4FORGE_RESULT_INTERNAL_ERROR;
    std::thread syncEventSubmitter([&] {
        syncEventResult = host.Events().Emit(eventEndpoint, &eventPayload, sizeof(eventPayload));
    });
    syncEventSubmitter.join();
    assert(syncEventResult == F4FORGE_RESULT_WRONG_THREAD);
    assert(eventCalls == 0);
    F4ForgeAsyncOperationHandle eventOperation = F4FORGE_INVALID_HANDLE;
    std::thread eventSubmitter([&] {
        assert(api.emitAsync(gameModule, eventEndpoint, &eventPayload, sizeof(eventPayload), &eventOperation)
            == F4FORGE_RESULT_SUCCESS);
    });
    eventSubmitter.join();
    assert(eventCalls == 0);
    assert(scheduler.RunOne());
    assert(api.waitOperation(eventOperation, F4FORGE_WAIT_INFINITE) == F4FORGE_RESULT_SUCCESS);
    assert(eventCalls == 1);
    assert(api.releaseOperation(eventOperation) == F4FORGE_RESULT_SUCCESS);
    api.unsubscribe(eventSubscription);
    F4ForgeModuleHandle autoReleaseModule = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(runtime, { "host.auto-release", sizeof("host.auto-release") - 1 }, 1,
        &autoReleaseModule) == F4FORGE_RESULT_SUCCESS);
    F4ForgeEndpointHandle autoReleaseEndpoint = F4FORGE_INVALID_HANDLE;
    auto autoReleaseDefinition = gameDefinition;
    autoReleaseDefinition.name = {
        "host.auto-release.endpoint", sizeof("host.auto-release.endpoint") - 1 };
    assert(api.registerEndpoint(autoReleaseModule, &autoReleaseDefinition, &autoReleaseEndpoint)
        == F4FORGE_RESULT_SUCCESS);
    F4ForgeAsyncOperationHandle autoReleaseOperation = F4FORGE_INVALID_HANDLE;
    std::thread autoReleaseSubmitter([&] {
        assert(api.invokeAsync(autoReleaseModule, autoReleaseEndpoint, nullptr, 0, &autoReleaseOperation)
            == F4FORGE_RESULT_SUCCESS);
    });
    autoReleaseSubmitter.join();
    assert(api.unregisterModule(autoReleaseModule) == F4FORGE_RESULT_SUCCESS);
    assert(api.pollOperation(autoReleaseOperation, &state) == F4FORGE_RESULT_INVALID_HANDLE);
    assert(scheduler.RunOne());
    assert(api.pollOperation(autoReleaseOperation, &state) == F4FORGE_RESULT_INVALID_HANDLE);
    F4ForgeModuleHandle targetModule = F4FORGE_INVALID_HANDLE;
    F4ForgeModuleHandle requesterModule = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(runtime, { "host.target", sizeof("host.target") - 1 }, 1, &targetModule)
        == F4FORGE_RESULT_SUCCESS);
    assert(api.registerModule(runtime, { "host.requester", sizeof("host.requester") - 1 }, 1, &requesterModule)
        == F4FORGE_RESULT_SUCCESS);
    F4ForgeEndpointDefinition targetDefinition = gameDefinition;
    targetDefinition.name = { "host.target.endpoint", sizeof("host.target.endpoint") - 1 };
    F4ForgeEndpointHandle targetEndpoint = F4FORGE_INVALID_HANDLE;
    assert(api.registerEndpoint(targetModule, &targetDefinition, &targetEndpoint) == F4FORGE_RESULT_SUCCESS);
    F4ForgeAsyncOperationHandle staleOperation = F4FORGE_INVALID_HANDLE;
    std::thread staleSubmitter([&] {
        assert(api.invokeAsync(requesterModule, targetEndpoint, nullptr, 0, &staleOperation)
            == F4FORGE_RESULT_SUCCESS);
    });
    staleSubmitter.join();
    assert(api.unregisterModule(targetModule) == F4FORGE_RESULT_SUCCESS);
    assert(scheduler.RunOne());
    assert(api.pollOperation(staleOperation, &state) == F4FORGE_RESULT_SUCCESS);
    assert(state == F4FORGE_ASYNC_OPERATION_COMPLETED);
    invocationResult = F4FORGE_RESULT_SUCCESS;
    assert(api.getOperationResult(staleOperation, &invocationResult, nullptr, 0, &responseSize)
        == F4FORGE_RESULT_SUCCESS);
    assert(invocationResult == F4FORGE_RESULT_STALE_HANDLE);
    assert(api.releaseOperation(staleOperation) == F4FORGE_RESULT_SUCCESS);
    assert(api.unregisterModule(requesterModule) == F4FORGE_RESULT_SUCCESS);
    F4ForgeAsyncOperationHandle shutdownOperation = F4FORGE_INVALID_HANDLE;
    std::thread shutdownSubmitter([&] {
        assert(api.invokeAsync(gameModule, gameEndpoint, nullptr, 0, &shutdownOperation)
            == F4FORGE_RESULT_SUCCESS);
    });
    shutdownSubmitter.join();
    host.Operations().Shutdown();
    assert(api.pollOperation(shutdownOperation, &state) == F4FORGE_RESULT_SUCCESS);
    assert(state == F4FORGE_ASYNC_OPERATION_CANCELLED);
    assert(api.releaseOperation(shutdownOperation) == F4FORGE_RESULT_SUCCESS);
    assert(api.unregisterModule(gameModule) == F4FORGE_RESULT_SUCCESS);

    F4ForgeModuleHandle shutdownModule = F4FORGE_INVALID_HANDLE;
    assert(api.registerModule(runtime, { "host.shutdown-owned", sizeof("host.shutdown-owned") - 1 }, 1,
        &shutdownModule) == F4FORGE_RESULT_SUCCESS);
    F4ForgeEndpointHandle shutdownEndpoint = F4FORGE_INVALID_HANDLE;
    auto shutdownDefinition = gameDefinition;
    shutdownDefinition.name = {
        "host.shutdown-owned.endpoint", sizeof("host.shutdown-owned.endpoint") - 1 };
    assert(api.registerEndpoint(shutdownModule, &shutdownDefinition, &shutdownEndpoint)
        == F4FORGE_RESULT_SUCCESS);
    const auto runtimeShutdownResult = runtimes.Shutdown(runtime);
    assert(runtimeShutdownResult == F4FORGE_RESULT_SUCCESS);
    const auto shutdownModuleActive = host.Modules().IsActive(shutdownModule);
    assert(!shutdownModuleActive);
    const auto providerModuleStillActive = host.Modules().IsActive(providerModule);
    assert(!providerModuleStillActive);
    assert(api.invoke(shutdownEndpoint, nullptr, 0, &gameResponse, sizeof(gameResponse), nullptr)
        == F4FORGE_RESULT_STALE_HANDLE);

    return 0;
}
