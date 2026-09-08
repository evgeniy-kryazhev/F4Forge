#include "async_operation.h"

#include <algorithm>
#include <chrono>
#include <cstring>

namespace f4forge::core {

AsyncOperation::AsyncOperation(
    F4ForgeModuleHandle requester,
    bool requiresGameThread,
    uint32_t responseCapacity,
    Executor executor)
    : _requester(requester),
      _requiresGameThread(requiresGameThread),
      _response(responseCapacity),
      _executor(std::move(executor))
{}

F4ForgeModuleHandle AsyncOperation::Requester() const noexcept
{
    return _requester;
}

bool AsyncOperation::RequiresGameThread() const noexcept
{
    return _requiresGameThread;
}

F4ForgeAsyncOperationState AsyncOperation::State() const noexcept
{
    std::lock_guard lock(_mutex);
    return _state;
}

void AsyncOperation::Run() noexcept
{
    {
        std::lock_guard lock(_mutex);
        if (_state != F4FORGE_ASYNC_OPERATION_PENDING) return;
        _state = F4FORGE_ASYNC_OPERATION_RUNNING;
    }

    F4ForgeResult result = F4FORGE_RESULT_INTERNAL_ERROR;
    try {
        result = _executor(*this);
    } catch (...) {
        result = F4FORGE_RESULT_INTERNAL_ERROR;
    }

    {
        std::lock_guard lock(_mutex);
        if (_state == F4FORGE_ASYNC_OPERATION_RUNNING) {
            _invocationResult = result;
            _state = F4FORGE_ASYNC_OPERATION_COMPLETED;
        }
    }
    _condition.notify_all();
}

F4ForgeResult AsyncOperation::Cancel() noexcept
{
    std::lock_guard lock(_mutex);
    if (_state == F4FORGE_ASYNC_OPERATION_PENDING) {
        _state = F4FORGE_ASYNC_OPERATION_CANCELLED;
        _condition.notify_all();
        return F4FORGE_RESULT_SUCCESS;
    }
    if (_state == F4FORGE_ASYNC_OPERATION_RUNNING) return F4FORGE_RESULT_NOT_CANCELLABLE;
    if (_state == F4FORGE_ASYNC_OPERATION_CANCELLED) return F4FORGE_RESULT_SUCCESS;
    return F4FORGE_RESULT_OPERATION_BUSY;
}

F4ForgeResult AsyncOperation::Wait(
    const GameThreadScheduler* scheduler,
    uint32_t timeoutMilliseconds) const noexcept
{
    std::unique_lock lock(_mutex);
    const auto terminal = [this] {
        return _state == F4FORGE_ASYNC_OPERATION_COMPLETED ||
            _state == F4FORGE_ASYNC_OPERATION_CANCELLED;
    };
    if (terminal()) return F4FORGE_RESULT_SUCCESS;
    if (_requiresGameThread && scheduler != nullptr && scheduler->IsGameThread())
        return F4FORGE_RESULT_WOULD_DEADLOCK;

    if (timeoutMilliseconds == F4FORGE_WAIT_INFINITE) {
        _condition.wait(lock, terminal);
        return F4FORGE_RESULT_SUCCESS;
    }
    if (!_condition.wait_for(lock, std::chrono::milliseconds(timeoutMilliseconds), terminal))
        return F4FORGE_RESULT_TIMEOUT;
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult AsyncOperation::GetResult(
    F4ForgeResult* invocationResult,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) const noexcept
{
    if (invocationResult == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    std::lock_guard lock(_mutex);
    if (_state == F4FORGE_ASYNC_OPERATION_PENDING || _state == F4FORGE_ASYNC_OPERATION_RUNNING)
        return F4FORGE_RESULT_NOT_READY;
    if (_state == F4FORGE_ASYNC_OPERATION_CANCELLED) return F4FORGE_RESULT_OPERATION_CANCELLED;

    if (responseSize != nullptr) *responseSize = _responseSize;
    if (responseCapacity < _responseSize || (_responseSize != 0 && response == nullptr))
        return F4FORGE_RESULT_BUFFER_TOO_SMALL;
    if (_responseSize != 0) std::memcpy(response, _response.data(), _responseSize);
    *invocationResult = _invocationResult;
    return F4FORGE_RESULT_SUCCESS;
}

void AsyncOperation::SetResponseSize(uint32_t size) noexcept
{
    std::lock_guard lock(_mutex);
    _responseSize = std::min(size, static_cast<uint32_t>(_response.size()));
}

void* AsyncOperation::ResponseData() noexcept
{
    return _response.empty() ? nullptr : _response.data();
}

uint32_t AsyncOperation::ResponseCapacity() const noexcept
{
    return static_cast<uint32_t>(_response.size());
}

AsyncOperationRegistry::AsyncOperationRegistry(GameThreadScheduler* scheduler) noexcept : _scheduler(scheduler) {}

void AsyncOperationRegistry::SetScheduler(GameThreadScheduler* scheduler) noexcept
{
    std::lock_guard lock(_mutex);
    _scheduler = scheduler;
}

GameThreadScheduler* AsyncOperationRegistry::Scheduler() const noexcept
{
    std::lock_guard lock(_mutex);
    return _scheduler;
}

F4ForgeResult AsyncOperationRegistry::Create(
    F4ForgeModuleHandle requester,
    bool requiresGameThread,
    uint32_t responseCapacity,
    AsyncOperation::Executor executor,
    F4ForgeAsyncOperationHandle* operation)
{
    if (operation == nullptr || !executor) return F4FORGE_RESULT_INVALID_ARGUMENT;
    auto object = std::make_shared<AsyncOperation>(
        requester, requiresGameThread, responseCapacity, std::move(executor));
    const auto handle = Publish(object);
    if (handle == F4FORGE_INVALID_HANDLE) return F4FORGE_RESULT_INTERNAL_ERROR;
    *operation = handle;

    GameThreadScheduler* scheduler;
    {
        std::lock_guard lock(_mutex);
        scheduler = _scheduler;
    }
    if (!requiresGameThread || (scheduler != nullptr && scheduler->IsGameThread())) {
        object->Run();
        return F4FORGE_RESULT_SUCCESS;
    }
    if (scheduler == nullptr) {
        object->Cancel();
        Release(handle);
        *operation = F4FORGE_INVALID_HANDLE;
        return F4FORGE_RESULT_SCHEDULER_UNAVAILABLE;
    }
    auto* job = new QueuedJob{ object };
    const auto result = scheduler->Post(
        &AsyncOperationRegistry::RunJob, job, &AsyncOperationRegistry::DestroyJob);
    if (result != F4FORGE_RESULT_SUCCESS) {
        DestroyJob(job);
        object->Cancel();
        Release(handle);
        *operation = F4FORGE_INVALID_HANDLE;
        return result;
    }
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult AsyncOperationRegistry::Poll(
    F4ForgeAsyncOperationHandle operation,
    F4ForgeAsyncOperationState* state) const noexcept
{
    if (state == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    const auto object = Acquire(operation);
    if (!object) return F4FORGE_RESULT_INVALID_HANDLE;
    *state = object->State();
    return F4FORGE_RESULT_SUCCESS;
}

F4ForgeResult AsyncOperationRegistry::Wait(
    F4ForgeAsyncOperationHandle operation,
    uint32_t timeoutMilliseconds) const noexcept
{
    const auto object = Acquire(operation);
    if (!object) return F4FORGE_RESULT_INVALID_HANDLE;
    const GameThreadScheduler* scheduler;
    {
        std::lock_guard lock(_mutex);
        scheduler = _scheduler;
    }
    return object->Wait(scheduler, timeoutMilliseconds);
}

F4ForgeResult AsyncOperationRegistry::GetResult(
    F4ForgeAsyncOperationHandle operation,
    F4ForgeResult* invocationResult,
    void* response,
    uint32_t responseCapacity,
    uint32_t* responseSize) const noexcept
{
    const auto object = Acquire(operation);
    if (!object) return F4FORGE_RESULT_INVALID_HANDLE;
    return object->GetResult(invocationResult, response, responseCapacity, responseSize);
}

F4ForgeResult AsyncOperationRegistry::Cancel(F4ForgeAsyncOperationHandle operation) const noexcept
{
    const auto object = Acquire(operation);
    if (!object) return F4FORGE_RESULT_INVALID_HANDLE;
    return object->Cancel();
}

F4ForgeResult AsyncOperationRegistry::Release(F4ForgeAsyncOperationHandle operation) noexcept
{
    std::shared_ptr<AsyncOperation> object;
    {
        std::lock_guard lock(_mutex);
        if (operation == F4FORGE_INVALID_HANDLE) return F4FORGE_RESULT_INVALID_HANDLE;
        const auto index = f4forge::HandleIndex(operation);
        if (index == 0 || index >= MaxOperations) return F4FORGE_RESULT_INVALID_HANDLE;
        const auto& slot = _slots[index];
        if (f4forge::HandleGeneration(operation) != slot.generation || !slot.operation)
            return F4FORGE_RESULT_INVALID_HANDLE;
        object = slot.operation;
    }

    const auto state = object->State();
    if (state != F4FORGE_ASYNC_OPERATION_COMPLETED && state != F4FORGE_ASYNC_OPERATION_CANCELLED)
        return F4FORGE_RESULT_OPERATION_BUSY;

    std::lock_guard lock(_mutex);
    const auto index = f4forge::HandleIndex(operation);
    auto& slot = _slots[index];
    if (f4forge::HandleGeneration(operation) != slot.generation || !slot.operation)
        return F4FORGE_RESULT_INVALID_HANDLE;
    slot.operation.reset();
    if (slot.generation == UINT32_MAX) return F4FORGE_RESULT_SUCCESS;
    ++slot.generation;
    _freeIndices.push_back(index);
    return F4FORGE_RESULT_SUCCESS;
}

void AsyncOperationRegistry::CancelRequester(F4ForgeModuleHandle requester) const noexcept
{
    std::vector<std::shared_ptr<AsyncOperation>> operations;
    {
        std::lock_guard lock(_mutex);
        for (uint32_t index = 1; index < _nextIndex; ++index) {
            if (_slots[index].operation && _slots[index].operation->Requester() == requester)
                operations.push_back(_slots[index].operation);
        }
    }
    for (const auto& operation : operations) operation->Cancel();
}

void AsyncOperationRegistry::RunJob(void* context) noexcept
{
    if (context == nullptr) return;
    auto* job = static_cast<QueuedJob*>(context);
    if (job->operation != nullptr) job->operation->Run();
}

void AsyncOperationRegistry::DestroyJob(void* context) noexcept
{
    delete static_cast<QueuedJob*>(context);
}

void AsyncOperationRegistry::Shutdown() noexcept
{
    std::vector<std::shared_ptr<AsyncOperation>> operations;
    GameThreadScheduler* scheduler;
    {
        std::lock_guard lock(_mutex);
        scheduler = _scheduler;
        for (uint32_t index = 1; index < _nextIndex; ++index) {
            if (_slots[index].operation) operations.push_back(_slots[index].operation);
        }
        _scheduler = nullptr;
    }
    for (const auto& operation : operations) operation->Cancel();
    if (scheduler != nullptr) scheduler->CancelPending();
}

std::shared_ptr<AsyncOperation> AsyncOperationRegistry::Acquire(
    F4ForgeAsyncOperationHandle operation) const noexcept
{
    if (operation == F4FORGE_INVALID_HANDLE) return {};
    const auto index = f4forge::HandleIndex(operation);
    if (index == 0 || index >= MaxOperations) return {};
    std::lock_guard lock(_mutex);
    const auto& slot = _slots[index];
    if (f4forge::HandleGeneration(operation) != slot.generation) return {};
    return slot.operation;
}

F4ForgeAsyncOperationHandle AsyncOperationRegistry::Publish(std::shared_ptr<AsyncOperation> operation)
{
    std::lock_guard lock(_mutex);
    uint32_t index = 0;
    if (!_freeIndices.empty()) {
        index = _freeIndices.back();
        _freeIndices.pop_back();
    } else {
        if (_nextIndex >= MaxOperations) return F4FORGE_INVALID_HANDLE;
        index = _nextIndex++;
    }
    _slots[index].operation = std::move(operation);
    return f4forge::MakeHandle(_slots[index].generation, index);
}

}
