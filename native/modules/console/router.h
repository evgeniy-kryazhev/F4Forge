#pragma once

#include "channel.h"
#include <vector>

namespace f4forge::console {

std::string NormalizeAlias(std::string_view alias);

struct Command {
    std::string alias;
    std::vector<std::string> arguments;
    std::string rawCommand;
    std::string rawArguments;
    std::string parseError;
};

Command ParseCommand(std::string_view text);

class Router final {
public:
    using Output = std::function<void(std::string_view)>;
    using IsReserved = std::function<bool(std::string_view)>;

    Router(Channel& channel, Output output, IsReserved isReserved);
    ~Router();
    Router(const Router&) = delete;
    Router& operator=(const Router&) = delete;

    // False means the engine must execute the original command unchanged.
    bool Execute(std::string_view text, std::uint32_t selectedReferenceFormId);

private:
    Channel& channel_;
    Output output_;
    IsReserved isReserved_;
    std::uint64_t validation_ = 0;
};

}
