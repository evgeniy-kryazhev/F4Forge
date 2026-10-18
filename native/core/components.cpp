#include "components.h"
#include <algorithm>
#include <stdexcept>
#include <set>
#include <tuple>

namespace f4forge {
namespace {

std::vector<Component>& Registrations()
{
    static std::vector<Component> registrations;
    return registrations;
}

}

ComponentRegistration::ComponentRegistration(Component component)
{
    Registrations().push_back(std::move(component));
}

void Components::Start(Channel& channel, const std::filesystem::path& directory)
{
    if (running_) {
        throw std::logic_error("Components are already started.");
    }
    auto registrations = Registrations();
    std::ranges::sort(registrations, [](const Component& left, const Component& right) {
        return std::tie(left.phase, left.name) < std::tie(right.phase, right.name);
    });
    std::set<std::string> names;
    for (const auto& component : registrations) {
        if (!names.insert(component.name).second || !component.start || !component.stop) {
            throw std::logic_error("Invalid or duplicate component: " + component.name);
        }
    }
    running_ = true;
    try {
        for (const auto& component : registrations) {
            // Include the component before Start so partial initialization is rolled back.
            started_.push_back(component);
            component.start(channel, directory);
        }
    } catch (...) {
        Stop();
        throw;
    }
}

void Components::Stop() noexcept
{
    for (auto iterator = started_.rbegin(); iterator != started_.rend(); ++iterator) {
        try {
            iterator->stop();
        } catch (...) {
            // Keep releasing the remaining SDKs after a component fails to stop.
        }
    }
    started_.clear();
    running_ = false;
}

}
