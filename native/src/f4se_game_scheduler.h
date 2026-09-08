#pragma once

#include "../core/async/game_scheduler.h"
#include "../abi/f4forge_abi.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

namespace f4forge::native {

class F4seGameScheduler final : public core::GameThreadScheduler {
public:
    F4seGameScheduler() noexcept;

    void CaptureGameThread() noexcept;
    bool IsGameThread() const noexcept override;
    F4ForgeResult Post(
        core::SchedulerJob job,
        void* context,
        core::SchedulerCleanup cleanup) noexcept override;
    void CancelPending() noexcept override;

    static int32_t F4FORGE_CALL CheckGameThread() noexcept;

private:
    struct Job final {
        core::SchedulerJob execute{};
        core::SchedulerCleanup cleanup{};
        void* context{};
        std::atomic<bool> cancelled{};
        std::atomic<bool> cleaned{};
    };

    static F4seGameScheduler*& Instance() noexcept;
    static void Cleanup(const std::shared_ptr<Job>& job) noexcept;

    std::mutex _mutex;
    std::vector<std::shared_ptr<Job>> _pending;
    DWORD _gameThreadId{};
    bool _accepting{ true };
};

}
