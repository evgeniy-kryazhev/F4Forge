#pragma once

#include "channel.h"
#include <stdexcept>
#include <utility>

namespace f4forge::logging {

using Sink = std::function<void(std::string_view level, std::string_view source, std::string_view message)>;

// Keeping the sink injectable lets the same handler run in tests without F4SE.
inline std::uint64_t Register(Channel& channel, Sink sink)
{
    if (!sink) {
        throw std::invalid_argument("A logging sink is required.");
    }
    return channel.Add("logging.write", [sink = std::move(sink)](const DataValue& arguments) {
        const auto level = arguments.at("level").get<std::string>();
        const auto source = arguments.at("source").get<std::string>();
        const auto message = arguments.at("message").get<std::string>();
        if (level != "info" && level != "warning" && level != "error") {
            throw std::invalid_argument("Unsupported logging level.");
        }
        if (source.empty() || source.size() > 256 || source.find_first_of("\r\n") != std::string::npos) {
            throw std::invalid_argument("A single-line source of at most 256 UTF-8 bytes is required.");
        }
        sink(level, source, message);
        return true;
    });
}

}
