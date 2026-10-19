#pragma once

#include "dispatch.h"
#include "../interception/registry.h"
#include <stdexcept>

namespace f4forge::console {

inline DataValue ValidateConsoleDecision(const DataValue& current, const DataValue& decision)
{
    const auto command = decision.at("command").get<std::string>();
    const auto skip = decision.at("skip").get<bool>();
    if (command.size() > Channel::MaxMessageBytes || command.find('\0') != std::string::npos) {
        throw std::invalid_argument("Invalid intercepted console command.");
    }
    auto next = current;
    next["command"] = command;
    next["skip"] = skip;
    return next;
}

inline Interception InterceptCommand(interception::Registry* registry, const char* input, std::uint32_t selected)
{
    if (registry == nullptr) {
        return Interception{input};
    }
    const auto result = registry->Apply("console.command.executing",
        {{"originalCommand", input}, {"command", input}, {"skip", false},
            {"selectedReferenceFormId", selected}}, ValidateConsoleDecision);
    return Interception{result.at("command").get<std::string>(), result.at("skip").get<bool>()};
}

}
