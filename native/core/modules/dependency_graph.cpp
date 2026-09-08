#include "dependency_graph.h"

#include <algorithm>
#include <queue>
#include <unordered_map>
#include <unordered_set>

namespace f4forge::core {

DependencyGraphResult DependencyGraph::Add(ModuleMetadata metadata)
{
    if (metadata.id.empty() || metadata.version == 0) return DependencyGraphResult::InvalidMetadata;
    const auto duplicate = std::find_if(_modules.begin(), _modules.end(), [&](const auto& current) {
        return current.id == metadata.id;
    });
    if (duplicate != _modules.end()) return DependencyGraphResult::DuplicateModule;
    _modules.push_back(std::move(metadata));
    return DependencyGraphResult::Success;
}

DependencyGraphResult DependencyGraph::Finalize(std::vector<std::string>& order) const
{
    order.clear();
    std::unordered_map<std::string, size_t> providers;
    std::unordered_set<std::string> moduleIds;
    std::unordered_set<std::string> capabilities{ "core" };
    for (size_t index = 0; index < _modules.size(); ++index) {
        providers.emplace(_modules[index].id, index);
        moduleIds.insert(_modules[index].id);
    }
    for (size_t index = 0; index < _modules.size(); ++index) {
        for (const auto& provided : _modules[index].provided) {
            if (provided.empty()) return DependencyGraphResult::InvalidMetadata;
            if (moduleIds.contains(provided)) return DependencyGraphResult::CapabilityCollision;
            if (!capabilities.insert(provided).second)
                return DependencyGraphResult::DuplicateCapability;
            providers.emplace(provided, index);
        }
    }

    std::vector<uint32_t> indegree(_modules.size(), 0);
    std::vector<std::vector<size_t>> outgoing(_modules.size());
    for (size_t index = 0; index < _modules.size(); ++index) {
        for (const auto& requirement : _modules[index].required) {
            size_t owner = 0;
            const auto module = providers.find(requirement);
            if (module != providers.end()) owner = module->second;
            else if (capabilities.contains(requirement)) continue;
            else return DependencyGraphResult::MissingDependency;
            if (owner == index) return DependencyGraphResult::CycleDetected;
            outgoing[owner].push_back(index);
            ++indegree[index];
        }
    }

    std::queue<size_t> ready;
    for (size_t index = 0; index < indegree.size(); ++index)
        if (indegree[index] == 0) ready.push(index);
    while (!ready.empty()) {
        const auto current = ready.front();
        ready.pop();
        order.push_back(_modules[current].id);
        for (const auto dependent : outgoing[current])
            if (--indegree[dependent] == 0) ready.push(dependent);
    }
    return order.size() == _modules.size() ? DependencyGraphResult::Success : DependencyGraphResult::CycleDetected;
}

void DependencyGraph::Clear() noexcept
{
    _modules.clear();
}

}
