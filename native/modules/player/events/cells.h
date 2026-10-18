#pragma once

#include "../../game_events/runtime.h"
#include "../../game_events/references.h"
#include <optional>

namespace f4forge::game_events {

class CellChanges final {
public:
    void Reset()
    {
        std::lock_guard lock(mutex_);
        previous_ = 0;
    }

    std::optional<DataValue> Enter(std::uint32_t actor, std::uint32_t cell, bool entering)
    {
        std::lock_guard lock(mutex_);
        if (actor != PlayerFormId || !entering || cell == 0 || cell == previous_) {
            return std::nullopt;
        }
        DataValue data = {{"previousCellFormId", previous_}, {"newCellFormId", cell}};
        previous_ = cell;
        return data;
    }

private:
    std::mutex mutex_;
    std::uint32_t previous_ = 0;
};

}
