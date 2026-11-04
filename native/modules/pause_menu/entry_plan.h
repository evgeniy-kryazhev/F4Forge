#pragma once

#include <cstddef>
#include <cstdint>
#include <map>
#include <optional>
#include <string>
#include <vector>

namespace f4forge::pause_menu
{
struct EntrySnapshot
{
    std::optional<std::string> scope;
    std::string label;
};

struct EntryPlan
{
    std::vector<std::uint32_t> removals;
    std::map<std::string, std::string> insertions;

    bool HasChanges() const
    {
        return !removals.empty() || !insertions.empty();
    }
};

inline EntryPlan PlanEntries(const std::vector<EntrySnapshot>& current,
                             const std::map<std::string, std::string>& registered)
{
    std::map<std::string, std::string> installed;
    EntryPlan plan;
    // Remove only owned entries, from the end so earlier indices stay valid.
    for (auto index = current.size(); index > 0; --index)
    {
        const auto& entry = current[index - 1];
        if (entry.scope)
        {
            installed.emplace(*entry.scope, entry.label);
            plan.removals.push_back(static_cast<std::uint32_t>(index - 1));
        }
    }
    if (installed == registered && plan.removals.size() == registered.size())
    {
        return {};
    }
    plan.insertions = registered;
    return plan;
}
} // namespace f4forge::pause_menu
