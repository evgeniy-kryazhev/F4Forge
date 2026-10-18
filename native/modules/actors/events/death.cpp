#include "death.h"
#include <RE/Fallout.h>

namespace f4forge::game_events {
namespace {

class DeathSink final : public RE::BSTEventSink<RE::TESDeathEvent> {
public:
    RE::BSEventNotifyControl ProcessEvent(const RE::TESDeathEvent& event, RE::BSTEventSource<RE::TESDeathEvent>*) override
    {
        Host().Invoke([&] {
            ActorDied(Host(), event.actorDying ? event.actorDying->GetFormID() : 0,
                event.actorKiller ? event.actorKiller->GetFormID() : 0, event.dead);
        });
        return RE::BSEventNotifyControl::kContinue;
    }
};

DeathSink sink;
std::mutex sourceMutex;
RE::BSTEventSource<RE::TESDeathEvent>* source = nullptr;

void Detach()
{
    std::lock_guard lock(sourceMutex);
    if (source != nullptr) {
        source->UnregisterSink(&sink);
        source = nullptr;
    }
}

const AdapterRegistration adapter({"death", [](Runtime& runtime, const Message&) {
    if (!runtime.DataReady()) {
        Detach();
        return;
    }
    std::lock_guard lock(sourceMutex);
    if (source == nullptr) {
        source = RE::TESDeathEvent::GetEventSource();
        if (source != nullptr) {
            source->RegisterSink(&sink);
        }
    }
}, Detach});

}
}
