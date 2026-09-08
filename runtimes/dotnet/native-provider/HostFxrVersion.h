#pragma once

#include <filesystem>
#include <optional>
#include <string_view>
#include <vector>

namespace f4forge::dotnet {

struct HostFxrVersion final {
    int major{};
    int minor{};
    int patch{};
    bool stable{};
};

std::optional<HostFxrVersion> ParseHostFxrVersion(std::wstring_view value);
std::filesystem::path SelectHighestHostFxrPath(const std::vector<std::filesystem::path>& directories);

}
