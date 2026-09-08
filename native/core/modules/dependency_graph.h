#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace f4forge::core {

struct ModuleMetadata final {
    std::string id;
    uint32_t version{};
    std::vector<std::string> required;
    std::vector<std::string> provided;
};

enum class DependencyGraphResult : uint32_t {
    Success,
    InvalidMetadata,
    DuplicateModule,
    DuplicateCapability,
    CapabilityCollision,
    MissingDependency,
    CycleDetected
};

class DependencyGraph final {
public:
    DependencyGraph() = default;

    DependencyGraphResult Add(ModuleMetadata metadata);
    DependencyGraphResult Finalize(std::vector<std::string>& order) const;
    void Clear() noexcept;

private:
    std::vector<ModuleMetadata> _modules;
};

}
