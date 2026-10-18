#include "components.h"
#include "logging.h"
#include <REX/REX.h>

#ifdef ERROR
#undef ERROR
#endif

namespace {

f4forge::Channel* activeChannel = nullptr;
std::uint64_t registration = 0;

void WriteLog(std::string_view level, std::string_view source, std::string_view message)
{
    // User text is a format argument, never a format string.
    if (level == "info") {
        REX::INFO("[{}] {}", source, message);
    } else if (level == "warning") {
        REX::WARN("[{}] {}", source, message);
    } else {
        REX::ERROR("[{}] {}", source, message);
    }
}

const f4forge::ComponentRegistration component({
    "logging",
    [](f4forge::Channel& channel, const std::filesystem::path&) {
        registration = f4forge::logging::Register(channel, WriteLog);
        activeChannel = &channel;
    },
    [] {
        if (activeChannel != nullptr) {
            activeChannel->Remove(registration);
            activeChannel = nullptr;
            registration = 0;
        }
    }
});

}
