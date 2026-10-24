#pragma once

#include "channel.h"
#include <optional>

namespace f4forge::ui {

struct Backend {
    std::function<bool()> isGameThread;
    // No value means the engine UI singleton is not available yet.
    std::function<std::optional<bool>()> pauseMenuOpen;
};

class Service final {
public:
    Service(Channel& channel, Backend backend);
    ~Service();
    Service(const Service&) = delete;
    Service& operator=(const Service&) = delete;

private:
    Channel& channel_;
    std::uint64_t registration_;
};

}
