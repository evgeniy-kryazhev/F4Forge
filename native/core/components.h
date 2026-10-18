#pragma once

#include "channel.h"
#include <filesystem>
#include <functional>
#include <vector>

namespace f4forge {

enum class ComponentPhase {
    NativeModules,
    Sdk
};

struct Component {
    std::string name;
    std::function<void(Channel&, const std::filesystem::path&)> start;
    std::function<void()> stop;
    ComponentPhase phase = ComponentPhase::NativeModules;
};

// A new native module or SDK adds its own registration in a new .cpp file.
class ComponentRegistration final {
public:
    explicit ComponentRegistration(Component component);
};

class Components final {
public:
    void Start(Channel& channel, const std::filesystem::path& directory);
    void Stop() noexcept;

private:
    std::vector<Component> started_;
    bool running_ = false;
};

}
