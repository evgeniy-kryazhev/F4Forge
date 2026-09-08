#include "modules/dependency_graph.h"

#include <cassert>

int main()
{
    f4forge::core::DependencyGraph graph;
    assert(graph.Add({ "forms", 1, { "core" }, { "core.forms" } })
        == f4forge::core::DependencyGraphResult::Success);
    assert(graph.Add({ "player", 1, { "core.forms" }, { "core.player" } })
        == f4forge::core::DependencyGraphResult::Success);
    std::vector<std::string> order;
    assert(graph.Finalize(order) == f4forge::core::DependencyGraphResult::Success);
    assert(order.size() == 2 && order[0] == "forms" && order[1] == "player");

    f4forge::core::DependencyGraph missing;
    assert(missing.Add({ "broken", 1, { "core.missing" }, {} })
        == f4forge::core::DependencyGraphResult::Success);
    assert(missing.Finalize(order) == f4forge::core::DependencyGraphResult::MissingDependency);

    f4forge::core::DependencyGraph cycle;
    assert(cycle.Add({ "a", 1, { "b" }, {} }) == f4forge::core::DependencyGraphResult::Success);
    assert(cycle.Add({ "b", 1, { "a" }, {} }) == f4forge::core::DependencyGraphResult::Success);
    assert(cycle.Finalize(order) == f4forge::core::DependencyGraphResult::CycleDetected);
    return 0;
}
