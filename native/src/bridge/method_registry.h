#pragma once

#include "channel.h"

#include <map>
#include <span>
#include <string>
#include <string_view>

namespace f4forge {

using MethodHandler = F4SEValue (*)(std::span<const F4SEValue> arguments) noexcept;

class MethodRegistry {
public:
    void Register(std::string name, MethodHandler handler);
    [[nodiscard]] MethodHandler Find(std::string_view name) const;

private:
    std::map<std::string, MethodHandler, std::less<>> methods_;
};

// The linked host supplies its bindings once, before the registry is published.
void RegisterHostMethods(MethodRegistry& registry);
void RegisterCommonLibMethods(MethodRegistry& registry);
const MethodRegistry& GetMethodRegistry();

}
