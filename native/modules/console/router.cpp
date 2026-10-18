#include "router.h"
#include <stdexcept>
#include <utility>

namespace f4forge::console {
namespace {

bool IsSpace(char character)
{
    return character == ' ' || character == '\t' || character == '\r' || character == '\n';
}

bool IsLetter(char character)
{
    return (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
}

}

std::string NormalizeAlias(std::string_view alias)
{
    if (alias.empty() || alias.size() > 64) {
        throw std::invalid_argument("Command alias must contain 1 to 64 ASCII characters.");
    }
    std::string normalized;
    normalized.reserve(alias.size());
    for (std::size_t index = 0; index < alias.size(); ++index) {
        const char character = alias[index];
        const bool validStart = IsLetter(character) || character == '_';
        const bool validContinuation = validStart || (character >= '0' && character <= '9') || character == '.' || character == '-';
        if (!(index == 0 ? validStart : validContinuation)) {
            throw std::invalid_argument("Command alias must match [A-Za-z_][A-Za-z0-9_.-]*.");
        }
        normalized.push_back(character >= 'A' && character <= 'Z' ? static_cast<char>(character + ('a' - 'A')) : character);
    }
    return normalized;
}

Command ParseCommand(std::string_view text)
{
    Command command;
    command.rawCommand = text;
    std::size_t position = 0;
    while (position < text.size() && IsSpace(text[position])) {
        ++position;
    }
    const auto aliasStart = position;
    while (position < text.size() && !IsSpace(text[position])) {
        ++position;
    }
    try {
        command.alias = NormalizeAlias(text.substr(aliasStart, position - aliasStart));
    } catch (const std::invalid_argument&) {
        return command;
    }
    while (position < text.size() && IsSpace(text[position])) {
        ++position;
    }
    command.rawArguments = text.substr(position);
    while (position < text.size()) {
        std::string argument;
        bool quoted = false;
        while (position < text.size()) {
            const char character = text[position];
            if (!quoted && IsSpace(character)) {
                break;
            }
            if (character == '"') {
                quoted = !quoted;
                ++position;
                continue;
            }
            if (quoted && character == '\\' && position + 1 < text.size()) {
                const char next = text[position + 1];
                if (next == '"' || next == '\\') {
                    argument.push_back(next);
                    position += 2;
                    continue;
                }
            }
            argument.push_back(character);
            ++position;
        }
        if (quoted) {
            command.parseError = "Unclosed double quote in command arguments.";
            return command;
        }
        command.arguments.push_back(std::move(argument));
        while (position < text.size() && IsSpace(text[position])) {
            ++position;
        }
    }
    return command;
}

Router::Router(Channel& channel, Output output, IsReserved isReserved)
    : channel_(channel), output_(std::move(output)), isReserved_(std::move(isReserved))
{
    if (!output_ || !isReserved_) {
        throw std::invalid_argument("Console output and reserved-command lookup are required.");
    }
    validation_ = channel_.Add("console.validate_alias", [this](const DataValue& arguments) {
        const auto alias = NormalizeAlias(arguments.at("alias").get<std::string>());
        if (isReserved_(alias)) {
            throw std::invalid_argument("Command alias belongs to a built-in game command: " + alias);
        }
        return true;
    });
}

Router::~Router()
{
    channel_.Remove(validation_);
}

bool Router::Execute(std::string_view text, std::uint32_t selectedReferenceFormId)
{
    const auto command = ParseCommand(text);
    if (command.alias.empty() || isReserved_(command.alias)) {
        return false;
    }
    const DataValue arguments{
        {"alias", command.alias},
        {"arguments", command.arguments},
        {"rawCommand", command.rawCommand},
        {"rawArguments", command.rawArguments},
        {"selectedReferenceFormId", selectedReferenceFormId},
        {"parseError", command.parseError}};
    const auto& serializer = channel_.Serializer();
    const auto request = serializer.Serialize({{"method", "console.command." + command.alias}, {"arguments", arguments}});
    const auto response = serializer.Deserialize(channel_.Call(request));
    if (!response.at("success").get<bool>()) {
        const auto& error = response.at("error");
        if (error.at("code") == "method_not_found") {
            return false;
        }
        output_("F4Forge: " + error.at("message").get<std::string>());
        return true;
    }
    try {
        // Validate the entire result before displaying anything, including errors
        // from other SDKs that register handlers on the same public channel.
        const auto lines = response.at("value").at("lines").get<std::vector<std::string>>();
        for (const auto& line : lines) {
            output_(line);
        }
    } catch (const std::exception& exception) {
        output_("F4Forge console result: " + std::string(exception.what()));
    }
    return true;
}

}
