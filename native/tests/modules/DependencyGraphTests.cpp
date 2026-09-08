#include "modules/dependency_graph.h"

#include <cassert>

int main()
{
    f4forge::core::DependencyGraph graph;
    const auto formsResult = graph.Add({ "forms", 1, { "core" }, { "core.forms" } });
    assert(formsResult == f4forge::core::DependencyGraphResult::Success);
    const auto playerResult = graph.Add({ "player", 1, { "core.forms" }, { "core.player" } });
    assert(playerResult == f4forge::core::DependencyGraphResult::Success);
    std::vector<std::string> order;
    assert(graph.Finalize(order) == f4forge::core::DependencyGraphResult::Success);
    assert(order.size() == 2 && order[0] == "forms" && order[1] == "player");

    f4forge::core::DependencyGraph missing;
    const auto missingAddResult = missing.Add({ "broken", 1, { "core.missing" }, {} });
    assert(missingAddResult == f4forge::core::DependencyGraphResult::Success);
    assert(missing.Finalize(order) == f4forge::core::DependencyGraphResult::MissingDependency);

    f4forge::core::DependencyGraph cycle;
    const auto firstCycleAddResult = cycle.Add({ "a", 1, { "b" }, {} });
    assert(firstCycleAddResult == f4forge::core::DependencyGraphResult::Success);
    const auto secondCycleAddResult = cycle.Add({ "b", 1, { "a" }, {} });
    assert(secondCycleAddResult == f4forge::core::DependencyGraphResult::Success);
    assert(cycle.Finalize(order) == f4forge::core::DependencyGraphResult::CycleDetected);
    return 0;
}
