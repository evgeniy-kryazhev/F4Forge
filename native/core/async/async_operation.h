#pragma once

#include "game_scheduler.h"
#include "f4forge_handles.h"
#include "f4forge_results.h"

#include <array>
#include <condition_variable>
#include <cstdint>
#include <functional>
#include <memory>
#include <mutex>
#include <vector>

namespace f4forge::core {

class AsyncOperation final {
public:
    using Executor = std::function<F4ForgeResult(AsyncOperation&)>;

    AsyncOperation(
        F4ForgeModuleHandle requester,
        bool requiresGameThread,
        uint32_t responseCapacity,
        Executor executor);

    AsyncOperation(const AsyncOperation&) = delete;
    AsyncOperation& operator=(const AsyncOperation&) = delete;

    F4ForgeModuleHandle Requester() const noexcept;
    bool RequiresGameThread() const noexcept;
    F4ForgeAsyncOperationState State() const noexcept;
    void Run() noexcept;
    F4ForgeResult Cancel() noexcept;
    F4ForgeResult Wait(const GameThreadScheduler* scheduler, uint32_t timeoutMilliseconds) const noexcept;
    F4ForgeResult GetResult(
        F4ForgeResult* invocationResult,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) const noexcept;
    void SetResponseSize(uint32_t size) noexcept;
    void* ResponseData() noexcept;
    uint32_t ResponseCapacity() const noexcept;

private:
    mutable std::mutex _mutex;
    mutable std::condition_variable _condition;
    F4ForgeModuleHandle _requester{};
    bool _requiresGameThread{};
    F4ForgeAsyncOperationState _state{ F4FORGE_ASYNC_OPERATION_PENDING };
    F4ForgeResult _invocationResult{ F4FORGE_RESULT_INTERNAL_ERROR };
    uint32_t _responseSize{};
    std::vector<uint8_t> _response;
    Executor _executor;
};

class AsyncOperationRegistry final {
public:
    static constexpr uint32_t MaxOperations = 4096;

    explicit AsyncOperationRegistry(GameThreadScheduler* scheduler = nullptr) noexcept;

    void SetScheduler(GameThreadScheduler* scheduler) noexcept;
    GameThreadScheduler* Scheduler() const noexcept;

    F4ForgeResult Create(
        F4ForgeModuleHandle requester,
        bool requiresGameThread,
        uint32_t responseCapacity,
        AsyncOperation::Executor executor,
        F4ForgeAsyncOperationHandle* operation);

    F4ForgeResult Poll(
        F4ForgeAsyncOperationHandle operation,
        F4ForgeAsyncOperationState* state) const noexcept;
    F4ForgeResult Wait(
        F4ForgeAsyncOperationHandle operation,
        uint32_t timeoutMilliseconds) const noexcept;
    F4ForgeResult GetResult(
        F4ForgeAsyncOperationHandle operation,
        F4ForgeResult* invocationResult,
        void* response,
        uint32_t responseCapacity,
        uint32_t* responseSize) const noexcept;
    F4ForgeResult Cancel(F4ForgeAsyncOperationHandle operation) const noexcept;
    F4ForgeResult Release(F4ForgeAsyncOperationHandle operation) noexcept;
    void CancelRequester(F4ForgeModuleHandle requester) const noexcept;
    void Shutdown() noexcept;

    static void RunJob(void* context) noexcept;
    static void DestroyJob(void* context) noexcept;

private:
    struct QueuedJob final {
        std::shared_ptr<AsyncOperation> operation;
    };

    struct Slot final {
        uint32_t generation{ 1 };
        std::shared_ptr<AsyncOperation> operation;
    };

    std::shared_ptr<AsyncOperation> Acquire(F4ForgeAsyncOperationHandle operation) const noexcept;
    F4ForgeAsyncOperationHandle Publish(std::shared_ptr<AsyncOperation> operation);

    mutable std::mutex _mutex;
    GameThreadScheduler* _scheduler{};
    std::array<Slot, MaxOperations> _slots{};
    std::vector<uint32_t> _freeIndices;
    uint32_t _nextIndex{ 1 };
};

}
