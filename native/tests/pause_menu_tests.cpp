#include "../modules/pause_menu/entry_plan.h"
#include <iostream>
#include <stdexcept>

using f4forge::pause_menu::EntrySnapshot;
using f4forge::pause_menu::PlanEntries;

void Require(bool condition, const char* message)
{
    if (!condition)
    {
        throw std::runtime_error(message);
    }
}

int main()
{
    try
    {
        const std::map<std::string, std::string> registrations{{"first", "MCM Redux"}, {"second", "Other mod"}};
        const std::vector<EntrySnapshot> vanilla{{std::nullopt, "Continue"}, {std::nullopt, "Save"}};
        const auto initial = PlanEntries(vanilla, registrations);
        Require(initial.removals.empty() && initial.insertions == registrations,
                "Initial population must preserve vanilla entries.");

        const std::vector<EntrySnapshot> populated{
            {std::nullopt, "Continue"}, {"first", "MCM Redux"}, {"second", "Other mod"}, {std::nullopt, "Save"}};
        Require(!PlanEntries(populated, registrations).HasChanges(),
                "An unchanged frame must not rebuild the list or reset focus.");
        Require(PlanEntries(vanilla, registrations).insertions == registrations,
                "InitList replacement must restore all consumers.");
        Require(PlanEntries({}, registrations).insertions == registrations,
                "Delayed empty-list initialization must retain registrations.");

        const auto disposeFirst = PlanEntries(populated, {{"second", "Other mod"}});
        Require(disposeFirst.removals == std::vector<std::uint32_t>{2, 1},
                "Owned entries must be removed in reverse order.");
        Require(disposeFirst.insertions.size() == 1 && disposeFirst.insertions.contains("second"),
                "Disposing one consumer must retain another.");

        auto duplicate = populated;
        duplicate.push_back({"first", "MCM Redux"});
        Require(PlanEntries(duplicate, registrations).removals.size() == 3,
                "Duplicate owned entries must be repaired.");

        const auto stop = PlanEntries(populated, {});
        Require(stop.removals.size() == 2 && stop.insertions.empty(), "Final disposal must remove only owned entries.");
        Require(!PlanEntries(vanilla, {}).HasChanges(), "No consumers must leave vanilla menus untouched.");
        Require(PlanEntries(populated, {{"first", "Новое имя"}, {"second", "Other mod"}}).HasChanges(),
                "Changed Unicode labels must refresh.");
        std::cout << "Pause-menu reconciliation checks passed\n";
        return 0;
    }
    catch (const std::exception& exception)
    {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
