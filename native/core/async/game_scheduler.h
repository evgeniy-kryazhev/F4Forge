#pragma once

#include "f4forge_results.h"

#include <cstdint>

namespace f4forge::core {

using SchedulerJob = void (*)(void*) noexcept;
using SchedulerCleanup = void (*)(void*) noexcept;

class GameThreadScheduler {
public:
    virtual ~GameThreadScheduler() = default;

    virtual bool IsGameThread() const noexcept = 0;
    // On success the scheduler owns context and calls cleanup exactly once after execution or
    // cancellation. On failure ownership remains with the caller and cleanup is not called.
    virtual F4ForgeResult Post(
        SchedulerJob job,
        void* context,
        SchedulerCleanup cleanup) noexcept = 0;
    virtual void CancelPending() noexcept = 0;
};

}
