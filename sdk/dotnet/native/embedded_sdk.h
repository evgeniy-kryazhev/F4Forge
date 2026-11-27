#pragma once

#include <filesystem>
#include <span>

namespace f4forge {

std::span<const char> EmbeddedSdkResource(const wchar_t* name);

// hostfxr opens its configuration by path. Each startup owns a private directory.
class TemporaryRuntimeConfig final {
public:
    TemporaryRuntimeConfig();
    ~TemporaryRuntimeConfig();
    TemporaryRuntimeConfig(const TemporaryRuntimeConfig&) = delete;
    TemporaryRuntimeConfig& operator=(const TemporaryRuntimeConfig&) = delete;

    void Write(std::span<const char> contents) const;
    const std::filesystem::path& Path() const noexcept { return path_; }

private:
    std::filesystem::path directory_;
    std::filesystem::path path_;
};

}
