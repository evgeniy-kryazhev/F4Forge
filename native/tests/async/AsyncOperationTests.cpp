#include "async/async_operation.h"
#include "test_harness.h"

#include <condition_variable>
#include <chrono>
#include <mutex>
#include <thread>
#include <vector>

namespace {

class TestScheduler final : public f4forge::core::GameThreadScheduler {
public:
    struct Job final {
        f4forge::core::SchedulerJob job{};
        void* context{};
        f4forge::core::SchedulerCleanup cleanup{};
    };

    bool IsGameThread() const noexcept override { return false; }

    F4ForgeResult Post(
        f4forge::core::SchedulerJob job,
        void* context,
        f4forge::core::SchedulerCleanup cleanup) noexcept override
    {
        std::lock_guard lock(_mutex);
        if (_rejectPosts) return F4FORGE_RESULT_SCHEDULER_UNAVAILABLE;
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

    void RejectPosts(bool reject) noexcept
    {
        std::lock_guard lock(_mutex);
        _rejectPosts = reject;
    }

private:
    mutable std::mutex _mutex;
    std::vector<Job> _jobs;
    bool _rejectPosts{};
};

constexpr F4ForgeModuleHandle requester = f4forge::MakeHandle(1, 1);

}

int main()
{
    f4forge::test::Context test;
    TestScheduler scheduler;
    f4forge::core::AsyncOperationRegistry operations(&scheduler);

    F4ForgeAsyncOperationHandle pending = F4FORGE_INVALID_HANDLE;
    F4FORGE_CHECK(test, operations.Create(requester, true, 0,
        [](f4forge::core::AsyncOperation&) { return F4FORGE_RESULT_SUCCESS; }, &pending)
        == F4FORGE_RESULT_SUCCESS);
    operations.CancelRequester(requester);
    F4ForgeAsyncOperationState state = F4FORGE_ASYNC_OPERATION_PENDING;
    F4FORGE_CHECK(test, operations.Poll(pending, &state) == F4FORGE_RESULT_INVALID_HANDLE);
    F4FORGE_CHECK(test, scheduler.RunOne());

    F4ForgeAsyncOperationHandle completed = F4FORGE_INVALID_HANDLE;
    F4FORGE_CHECK(test, operations.Create(requester, false, 0,
        [](f4forge::core::AsyncOperation&) { return F4FORGE_RESULT_SUCCESS; }, &completed)
        == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, operations.Poll(completed, &state) == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, state == F4FORGE_ASYNC_OPERATION_COMPLETED);
    operations.CancelRequester(requester);
    F4FORGE_CHECK(test, operations.Poll(completed, &state) == F4FORGE_RESULT_INVALID_HANDLE);

    std::mutex runningMutex;
    std::condition_variable runningCondition;
    bool running = false;
    bool finish = false;
    F4ForgeAsyncOperationHandle runningOperation = F4FORGE_INVALID_HANDLE;
    F4FORGE_CHECK(test, operations.Create(requester, true, 0,
        [&](f4forge::core::AsyncOperation&) {
            {
                std::lock_guard lock(runningMutex);
                running = true;
            }
            runningCondition.notify_all();
            std::unique_lock lock(runningMutex);
            runningCondition.wait(lock, [&] { return finish; });
            return F4FORGE_RESULT_SUCCESS;
        }, &runningOperation) == F4FORGE_RESULT_SUCCESS);
    std::thread executor([&] { scheduler.RunOne(); });
    {
        std::unique_lock lock(runningMutex);
        F4FORGE_CHECK(test, runningCondition.wait_for(lock, std::chrono::seconds(10), [&] { return running; }));
    }
    operations.CancelRequester(requester);
    {
        std::lock_guard lock(runningMutex);
        finish = true;
    }
    runningCondition.notify_all();
    executor.join();
    F4FORGE_CHECK(test, operations.Poll(runningOperation, &state) == F4FORGE_RESULT_INVALID_HANDLE);

    F4ForgeAsyncOperationHandle cancelled = F4FORGE_INVALID_HANDLE;
    F4FORGE_CHECK(test, operations.Create(requester, true, 0,
        [](f4forge::core::AsyncOperation&) { return F4FORGE_RESULT_SUCCESS; }, &cancelled)
        == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, operations.Cancel(cancelled) == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, operations.Wait(cancelled, 0) == F4FORGE_RESULT_SUCCESS);
    operations.CancelRequester(requester);
    F4FORGE_CHECK(test, operations.Poll(cancelled, &state) == F4FORGE_RESULT_INVALID_HANDLE);

    scheduler.RejectPosts(true);
    F4ForgeAsyncOperationHandle rejected = F4FORGE_INVALID_HANDLE;
    F4FORGE_CHECK(test, operations.Create(requester, true, 0,
        [](f4forge::core::AsyncOperation&) { return F4FORGE_RESULT_SUCCESS; }, &rejected)
        == F4FORGE_RESULT_SCHEDULER_UNAVAILABLE);
    F4FORGE_CHECK(test, rejected == F4FORGE_INVALID_HANDLE);
    scheduler.RejectPosts(false);

    for (uint32_t index = 0; index < 5000; ++index) {
        F4ForgeAsyncOperationHandle operation = F4FORGE_INVALID_HANDLE;
        F4FORGE_CHECK(test, operations.Create(requester, false, 0,
            [](f4forge::core::AsyncOperation&) { return F4FORGE_RESULT_SUCCESS; }, &operation)
            == F4FORGE_RESULT_SUCCESS);
        operations.CancelRequester(requester);
        F4FORGE_CHECK(test, operations.Poll(operation, &state) == F4FORGE_RESULT_INVALID_HANDLE);
    }

    return test.Failures() == 0 ? 0 : 1;
}
