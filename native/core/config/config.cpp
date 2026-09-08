#include "config.h"

#include <algorithm>
#include <cctype>
#include <fstream>
#include <string_view>
#include <utility>

namespace f4forge::core {
namespace {

std::string Trim(std::string value)
{
    const auto notSpace = [](unsigned char character) { return std::isspace(character) == 0; };
    value.erase(value.begin(), std::find_if(value.begin(), value.end(), notSpace));
    value.erase(std::find_if(value.rbegin(), value.rend(), notSpace).base(), value.end());
    return value;
}

std::string Unquote(std::string value)
{
    value = Trim(std::move(value));
    if (value.size() >= 2 && value.front() == '"' && value.back() == '"')
        return value.substr(1, value.size() - 2);
    return value;
}

std::filesystem::path ResolvePath(const std::filesystem::path& value, const std::filesystem::path& base)
{
    if (value.empty()) return {};
    return value.is_absolute() ? value.lexically_normal() : (base / value).lexically_normal();
}

}

FrameworkConfig ConfigLoader::Defaults(const std::filesystem::path& frameworkDirectory)
{
    FrameworkConfig config;
    config.pluginDirectory = frameworkDirectory / L"Plugins";
    config.runtimeDirectory = frameworkDirectory;
    return config;
}

FrameworkConfig ConfigLoader::Load(
    const std::filesystem::path& file,
    const std::filesystem::path& frameworkDirectory)
{
    auto config = Defaults(frameworkDirectory);
    std::ifstream input(file);
    if (!input) return config;

    std::string section;
    std::string line;
    while (std::getline(input, line)) {
        const auto comment = line.find('#');
        if (comment != std::string::npos) line.resize(comment);
        line = Trim(std::move(line));
        if (line.empty()) continue;
        if (line.front() == '[' && line.back() == ']') {
            section = Trim(line.substr(1, line.size() - 2));
            continue;
        }
        const auto separator = line.find('=');
        if (separator == std::string::npos) continue;
        const auto key = Trim(line.substr(0, separator));
        const auto value = Unquote(line.substr(separator + 1));
        if (section == "plugins" && key == "directory")
            config.pluginDirectory = ResolvePath(value, frameworkDirectory);
        else if (section == "runtimes" && key == "directory")
            config.runtimeDirectory = ResolvePath(value, frameworkDirectory);
        else if (section == "framework" && key == "log_level")
            config.logLevel = value;
    }
    return config;
}

}
