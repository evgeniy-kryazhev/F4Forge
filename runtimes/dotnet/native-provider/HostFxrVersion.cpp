#include "HostFxrVersion.h"

#include <algorithm>
#include <cwctype>

namespace f4forge::dotnet {
namespace {

bool ReadNumber(std::wstring_view value, size_t& offset, int& result)
{
    if (offset >= value.size() || !std::iswdigit(value[offset])) return false;
    result = 0;
    while (offset < value.size() && std::iswdigit(value[offset])) {
        result = result * 10 + (value[offset] - L'0');
        ++offset;
    }
    return true;
}

bool IsHigher(const HostFxrVersion& left, const HostFxrVersion& right)
{
    if (left.major != right.major) return left.major > right.major;
    if (left.minor != right.minor) return left.minor > right.minor;
    if (left.patch != right.patch) return left.patch > right.patch;
    return left.stable && !right.stable;
}

}

std::optional<HostFxrVersion> ParseHostFxrVersion(std::wstring_view value)
{
    HostFxrVersion version{};
    size_t offset = 0;
    if (!ReadNumber(value, offset, version.major) || offset >= value.size() || value[offset++] != L'.')
        return std::nullopt;
    if (!ReadNumber(value, offset, version.minor) || offset >= value.size() || value[offset++] != L'.')
        return std::nullopt;
    if (!ReadNumber(value, offset, version.patch)) return std::nullopt;
    version.stable = offset == value.size();
    if (!version.stable && value[offset] != L'-') return std::nullopt;
    return version;
}

std::filesystem::path SelectHighestHostFxrPath(const std::vector<std::filesystem::path>& directories)
{
    std::filesystem::path selected;
    std::optional<HostFxrVersion> selectedVersion;
    for (const auto& directory : directories) {
        const auto parsed = ParseHostFxrVersion(directory.filename().wstring());
        if (!parsed || !std::filesystem::exists(directory / L"hostfxr.dll")) continue;
        if (!selectedVersion || IsHigher(*parsed, *selectedVersion)) {
            selectedVersion = parsed;
            selected = directory / L"hostfxr.dll";
        }
    }
    return selected;
}

}
