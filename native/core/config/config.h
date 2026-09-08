#pragma once

#include <filesystem>
#include <string>

namespace f4forge::core {

struct FrameworkConfig final {
    std::filesystem::path pluginDirectory;
    std::filesystem::path runtimeDirectory;
    std::string logLevel = "info";
};

class ConfigLoader final {
public:
    static FrameworkConfig Defaults(const std::filesystem::path& frameworkDirectory);
    static FrameworkConfig Load(
        const std::filesystem::path& file,
        const std::filesystem::path& frameworkDirectory);
};

}
