#pragma once

#include "f4forge_runtime_abi.h"

#include <array>
#include <cstdint>
#include <filesystem>
#include <memory>
#include <mutex>
#include <string>

namespace f4forge::core {

struct RuntimeProvider final {
    F4ForgeRuntimeInfo info{};
    F4ForgeRuntimeProvider provider{};
    void* nativeModule{};
};

struct RuntimeInstance final {
    enum class State : uint32_t {
        Created,
        Initializing,
        Active,
        ShuttingDown,
        Inactive,
        Failed
    };

    F4ForgeRuntimeHandle handle{};
    const RuntimeProvider* provider{};
    State state = State::Created;
};

class RuntimeManager final {
public:
    static constexpr uint32_t MaxProviders = 32;
    static constexpr uint32_t MaxRuntimes = 32;

    RuntimeManager() noexcept = default;
    RuntimeManager(const RuntimeManager&) = delete;
    RuntimeManager& operator=(const RuntimeManager&) = delete;

    F4ForgeResult RegisterProvider(const RuntimeProvider& provider);
    uint32_t DiscoverDirectory(const std::filesystem::path& directory);
    F4ForgeResult Initialize(
        F4ForgeStringView id,
        const F4ForgeHostApi* host,
        F4ForgeStringView pluginDirectory,
        F4ForgeStringView configDirectory,
        F4ForgeRuntimeHandle* runtime);
    uint32_t InitializeAll(
        const F4ForgeHostApi* host,
        F4ForgeStringView pluginDirectory,
        F4ForgeStringView configDirectory);
    F4ForgeResult Shutdown(F4ForgeRuntimeHandle runtime);
    RuntimeInstance* Find(F4ForgeRuntimeHandle runtime) noexcept;
    bool IsActive(F4ForgeRuntimeHandle runtime) const noexcept;
    bool HasProvider(F4ForgeStringView id) const noexcept;
    uint32_t ProviderCount() const noexcept;

private:
    static bool IsValidProvider(const RuntimeProvider& provider) noexcept;
    static bool Equal(F4ForgeStringView left, F4ForgeStringView right) noexcept;
    RuntimeInstance* FindUnlocked(F4ForgeRuntimeHandle runtime) const noexcept;

    mutable std::mutex _mutex;
    std::array<std::unique_ptr<RuntimeProvider>, MaxProviders> _providers{};
    std::array<std::unique_ptr<RuntimeInstance>, MaxRuntimes> _runtimes{};
    uint32_t _providerCount = 0;
    uint32_t _runtimeCount = 0;
    uint64_t _nextRuntimeHandle = 1;
};

}
