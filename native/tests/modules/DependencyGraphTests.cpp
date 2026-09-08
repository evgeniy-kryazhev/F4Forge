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

    f4forge::core::DependencyGraph duplicateCapability;
    const auto duplicateFirst = duplicateCapability.Add({ "a", 1, {}, { "shared" } });
    const auto duplicateSecond = duplicateCapability.Add({ "b", 1, {}, { "shared" } });
    assert(duplicateFirst == f4forge::core::DependencyGraphResult::Success);
    assert(duplicateSecond == f4forge::core::DependencyGraphResult::Success);
    assert(duplicateCapability.Finalize(order) ==
        f4forge::core::DependencyGraphResult::DuplicateCapability);

    f4forge::core::DependencyGraph capabilityCollision;
    const auto collisionFirst = capabilityCollision.Add({ "a", 1, {}, {} });
    const auto collisionSecond = capabilityCollision.Add({ "b", 1, {}, { "a" } });
    assert(collisionFirst == f4forge::core::DependencyGraphResult::Success);
    assert(collisionSecond == f4forge::core::DependencyGraphResult::Success);
    assert(capabilityCollision.Finalize(order) ==
        f4forge::core::DependencyGraphResult::CapabilityCollision);
    return 0;
}
