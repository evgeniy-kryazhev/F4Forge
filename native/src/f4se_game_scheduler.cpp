#include "f4se_game_scheduler.h"

#include <F4SE/API.h>

#include <algorithm>

namespace f4forge::native {

F4seGameScheduler::F4seGameScheduler() noexcept
{
    Instance() = this;
}

void F4seGameScheduler::CaptureGameThread() noexcept
{
    _gameThreadId = GetCurrentThreadId();
}

bool F4seGameScheduler::IsGameThread() const noexcept
{
    return _gameThreadId != 0 && GetCurrentThreadId() == _gameThreadId;
}

F4ForgeResult F4seGameScheduler::Post(
    core::SchedulerJob job,
    void* context,
    core::SchedulerCleanup cleanup) noexcept
{
    if (job == nullptr || cleanup == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    const auto* taskInterface = F4SE::GetTaskInterface();
    if (taskInterface == nullptr) return F4FORGE_RESULT_SCHEDULER_UNAVAILABLE;

    std::shared_ptr<Job> item;
    try {
        item = std::make_shared<Job>();
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
    item->execute = job;
    item->cleanup = cleanup;
    item->context = context;
    {
        std::lock_guard lock(_mutex);
        if (!_accepting) return F4FORGE_RESULT_SCHEDULER_UNAVAILABLE;
        try {
            _pending.push_back(item);
            taskInterface->AddTask([this, item] {
                {
                    std::lock_guard lock(_mutex);
                    _pending.erase(std::remove(_pending.begin(), _pending.end(), item), _pending.end());
                }
                if (!item->cancelled.load(std::memory_order_acquire)) item->execute(item->context);
                Cleanup(item);
            });
        } catch (...) {
            _pending.erase(std::remove(_pending.begin(), _pending.end(), item), _pending.end());
            return F4FORGE_RESULT_INTERNAL_ERROR;
        }
    }
    return F4FORGE_RESULT_SUCCESS;
}

void F4seGameScheduler::CancelPending() noexcept
{
    std::vector<std::shared_ptr<Job>> pending;
    {
        std::lock_guard lock(_mutex);
        _accepting = false;
        pending.swap(_pending);
        for (const auto& item : pending) item->cancelled.store(true, std::memory_order_release);
    }
    for (const auto& item : pending) Cleanup(item);
}

int32_t F4FORGE_CALL F4seGameScheduler::CheckGameThread() noexcept
{
    const auto* scheduler = Instance();
    return scheduler != nullptr && scheduler->IsGameThread() ? 1 : 0;
}

F4seGameScheduler*& F4seGameScheduler::Instance() noexcept
{
    static F4seGameScheduler* instance{};
    return instance;
}

void F4seGameScheduler::Cleanup(const std::shared_ptr<Job>& job) noexcept
{
    if (!job->cleaned.exchange(true, std::memory_order_acq_rel)) job->cleanup(job->context);
}

}
