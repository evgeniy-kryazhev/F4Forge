#include "../diagnostics/store.h"
#include "../extensions/transport.h"
#include "../game_events/vtable_hook.h"
#include "components.h"
#include "entry_plan.h"
#include <Scaleform/G/GFx_ASMovieRootBase.h>
#include <Scaleform/G/GFx_FunctionHandler.h>

namespace f4forge::pause_menu
{
namespace
{
using Value = Scaleform::GFx::Value;
std::atomic<Channel*> channel = nullptr;
std::map<std::string, std::string> entries;
std::vector<std::uint64_t> routes;
std::uint64_t menuSubscription = 0;
constexpr auto ListPath = "root.Menu_mc.MainPanel_mc.List_mc";
using AdvanceHandler = void (*)(RE::IMenu*, float, std::uint64_t);
game_events::VtableHook<AdvanceHandler> advanceHook;
std::uintptr_t advanceTable = 0;
bool reportedMissingList = false;

class PressHandler final : public Scaleform::GFx::FunctionHandler
{
  public:
    void Call(const Params& parameters) override
    {
        game_events::Host().Invoke([&] {
            if (parameters.argCount == 0 || channel == nullptr)
            {
                return;
            }
            Value list;
            Value selected;
            Value scope;
            if (!parameters.args[0].GetMember("target", &list) || !list.GetMember("selectedEntry", &selected) ||
                !selected.IsObject() || !selected.GetMember("f4forgeScope", &scope) || !scope.IsString())
            {
                return;
            }
            parameters.args[0].Invoke("stopImmediatePropagation", nullptr, nullptr, 0);
            const std::string copy = scope.GetString();
            extensions::Queue([copy] {
                auto* owner = channel.load();
                if (owner == nullptr)
                {
                    return;
                }
                owner->Dispatch([&](const DataValue&) -> DataValue {
                    if (entries.contains(copy))
                    {
                        game_events::Host().Publish("ui.pause.selected", {{"scope", copy}});
                    }
                    return nullptr;
                });
            });
        });
    }
};

void Refresh(RE::IMenu& menu)
{
    if (!menu.uiMovie)
    {
        return;
    }
    auto* movie = menu.uiMovie.get();
    Value list;
    Value items;
    if (!movie->GetVariable(&list, ListPath) || !list.GetMember("entryList", &items) || !items.IsArray())
    {
        if (!entries.empty() && !reportedMissingList)
        {
            REX::WARN("Pause extensions: waiting for {} after movie advance", ListPath);
            reportedMissingList = true;
        }
        return;
    }
    reportedMissingList = false;
    std::vector<EntrySnapshot> snapshot;
    for (std::uint32_t index = 0; index < items.GetArraySize(); ++index)
    {
        Value item;
        Value scope;
        Value label;
        EntrySnapshot entry;
        if (items.GetElement(index, &item) && item.IsObject())
        {
            if (item.GetMember("f4forgeScope", &scope) && scope.IsString())
            {
                entry.scope = scope.GetString();
            }
            if (item.GetMember("text", &label) && label.IsString())
            {
                entry.label = label.GetString();
            }
        }
        snapshot.push_back(std::move(entry));
    }
    const auto plan = PlanEntries(snapshot, entries);
    if (!plan.HasChanges())
    {
        return;
    }
    Value root;
    Value installed;
    // MainMenu is sealed; the timeline root can retain our native listener.
    if (!movie->GetVariable(&root, "root"))
    {
        throw std::runtime_error("Pause menu timeline root is unavailable.");
    }
    if (!root.GetMember("f4forgePauseListener", &installed) ||
        (!installed.IsObject() && installed.GetType() != Value::ValueType::kClosure))
    {
        auto handler = std::make_unique<PressHandler>();
        Value callback;
        movie->CreateFunction(&callback, handler.get());
        handler.release()->Release();
        if (!root.SetMember("f4forgePauseListener", callback))
        {
            throw std::runtime_error("Cannot retain the pause-menu selection callback.");
        }
        const Value arguments[]{Value("BSScrollingList::itemPress"), callback, Value(true), Value(INT32_MAX)};
        if (!root.Invoke("addEventListener", nullptr, arguments, 4))
        {
            throw std::runtime_error("Cannot attach the pause-menu selection callback.");
        }
    }
    for (const auto index : plan.removals)
    {
        const Value arguments[]{Value(index), Value(1)};
        if (!items.Invoke("splice", nullptr, arguments, 2))
        {
            throw std::runtime_error("Cannot remove an owned pause-menu entry.");
        }
    }
    std::uint32_t index = 0x7f000000;
    std::uint32_t insertionIndex = items.GetArraySize() == 0 ? 0 : 1;
    for (const auto& [scope, label] : plan.insertions)
    {
        Value item;
        movie->CreateObject(&item);
        Value ownedLabel;
        Value ownedScope;
        movie->asMovieRoot->CreateString(&ownedLabel, label.c_str());
        movie->asMovieRoot->CreateString(&ownedScope, scope.c_str());
        item.SetMember("text", ownedLabel);
        item.SetMember("index", Value(index++));
        item.SetMember("f4forgeScope", ownedScope);
        const Value arguments[]{Value(insertionIndex++), Value(0), item};
        if (!items.Invoke("splice", nullptr, arguments, 3))
        {
            throw std::runtime_error("Cannot insert a registered pause-menu entry.");
        }
    }
    if (!list.Invoke("InvalidateData", nullptr, nullptr, 0))
    {
        throw std::runtime_error("Cannot invalidate the pause-menu entry list.");
    }
    REX::INFO("Pause extensions: synchronized {} registered entries; list contains {} items", entries.size(),
              items.GetArraySize());
}

void Advance(RE::IMenu* menu, float delta, std::uint64_t time)
{
    advanceHook.Original()(menu, delta, time);
    // InitList can replace the entire array after the open notification and
    // again while the menu is alive. Reconcile after the engine advances it.
    game_events::Host().Invoke([&] {
        auto* owner = channel.load();
        if (owner == nullptr)
        {
            return;
        }
        owner->Dispatch([&](const DataValue&) -> DataValue {
            Refresh(*menu);
            return nullptr;
        });
    });
}

AdvanceHandler ReadAdvance()
{
    return reinterpret_cast<AdvanceHandler>(diagnostics::CheckVtableSlot(advanceTable, 4));
}

void WriteAdvance(AdvanceHandler handler)
{
    REL::Relocation<std::uintptr_t> table(advanceTable);
    table.write_vfunc(4, handler);
}

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    advanceTable = REL::Relocation<std::uintptr_t>(RE::VTABLE::PauseMenu[0]).address();
    diagnostics::CheckAndInstall("ui.pause.advance", "vtable",
                                 [] { advanceHook.Attach(Advance, ReadAdvance, WriteAdvance); });
    REX::INFO("Pause extensions: movie-advance integration installed (revision 2)");
    routes.push_back(owner.Add("ui.pause.register", [](const DataValue& request) -> DataValue {
        const auto scope = request.at("scope").get<std::string>();
        const auto label = request.at("label").get<std::string>();
        if (scope.empty() || label.empty() || entries.contains(scope))
        {
            throw std::invalid_argument("A unique scope and nonempty label are required.");
        }
        entries.emplace(scope, label);
        REX::INFO("Pause extensions: registered '{}' ({})", label, scope);
        return true;
    }));
    routes.push_back(owner.Add("ui.pause.release", [](const DataValue& request) -> DataValue {
        entries.erase(request.at("scope").get<std::string>());
        return true;
    }));
    menuSubscription = owner.Events().Subscribe("game.menu.changed", [](const DataValue& notification) {
        if (notification.at("menuName") == "PauseMenu" && notification.at("opening").get<bool>())
        {
            REX::INFO("Pause extensions: PauseMenu opened; {} registered entries", entries.size());
        }
    });
}

void Stop()
{
    auto* owner = channel.exchange(nullptr);
    if (owner == nullptr)
    {
        return;
    }
    owner->Events().Unsubscribe(menuSubscription);
    advanceHook.Detach(Advance, ReadAdvance, WriteAdvance);
    owner->Dispatch([](const DataValue&) -> DataValue {
        entries.clear();
        return nullptr;
    });
    for (const auto route : routes)
    {
        owner->Remove(route);
    }
    routes.clear();
}

const ComponentRegistration component({"ui.pause.extensions", Start, Stop});
} // namespace
} // namespace f4forge::pause_menu
