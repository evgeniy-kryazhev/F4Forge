#include "cells.h"
#include <RE/Fallout.h>

namespace f4forge::game_events {
namespace {

CellChanges changes;

class CellSink final : public RE::BSTEventSink<RE::BGSActorCellEvent> {
public:
    RE::BSEventNotifyControl ProcessEvent(const RE::BGSActorCellEvent& event, RE::BSTEventSource<RE::BGSActorCellEvent>*) override
    {
        Host().Invoke([&] {
            const auto actor = event.actor.get();
            const auto data = changes.Enter(actor ? actor->GetFormID() : 0, event.cellID,
                event.flags == RE::BGSActorCellEvent::CellFlag::kEnter);
            if (data) {
                Host().Publish("game.player.cell.changed", *data);
            }
        });
        return RE::BSEventNotifyControl::kContinue;
    }
};

CellSink sink;
std::mutex sourceMutex;
RE::BSTEventSource<RE::BGSActorCellEvent>* source = nullptr;

void Detach()
{
    std::lock_guard lock(sourceMutex);
    if (source != nullptr) {
        source->UnregisterSink(&sink);
        source = nullptr;
    }
    changes.Reset();
}

const AdapterRegistration adapter({"cells", [](Runtime& runtime, const Message& message) {
    if (message.Is(MessageType::PreLoadGame) || message.Is(MessageType::NewGame)) {
        changes.Reset();
    }
    if (!runtime.DataReady()) {
        Detach();
        return;
    }
    std::lock_guard lock(sourceMutex);
    if (source == nullptr) {
        const auto player = RE::PlayerCharacter::GetSingleton();
        if (player != nullptr) {
            source = static_cast<RE::BSTEventSource<RE::BGSActorCellEvent>*>(player);
            source->RegisterSink(&sink);
        }
    }
}, Detach});

}
}
