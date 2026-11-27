#include "method_registry.h"

#include <stdexcept>
#include <utility>

namespace f4forge {

void MethodRegistry::Register(std::string name, MethodHandler handler)
{
    if (name.empty() || name.find('\0') != std::string::npos || handler == nullptr) {
        throw std::invalid_argument("A bridge method needs a valid name and handler.");
    }

    const auto [entry, inserted] = methods_.try_emplace(std::move(name), handler);
    if (!inserted) {
        throw std::invalid_argument("Duplicate bridge method: " + entry->first);
    }
}

MethodHandler MethodRegistry::Find(std::string_view name) const
{
    const auto entry = methods_.find(name);
    if (entry == methods_.end()) {
        return nullptr;
    }
    return entry->second;
}

const MethodRegistry& GetMethodRegistry()
{
    static const MethodRegistry methods = [] {
        MethodRegistry registry;
        RegisterHostMethods(registry);
        return registry;
    }();
    return methods;
}

}
