#include "menus.h"
#include <RE/Fallout.h>

namespace f4forge::game_events {
namespace {

class MenuSink final : public RE::BSTEventSink<RE::MenuOpenCloseEvent> {
public:
    RE::BSEventNotifyControl ProcessEvent(const RE::MenuOpenCloseEvent& event, RE::BSTEventSource<RE::MenuOpenCloseEvent>*) override
    {
        Host().Invoke([&] {
            const auto* name = event.menuName.c_str();
            MenuChanged(Host(), name != nullptr ? name : "", event.opening);
        });
        return RE::BSEventNotifyControl::kContinue;
    }
};

MenuSink sink;
std::mutex sourceMutex;
RE::BSTEventSource<RE::MenuOpenCloseEvent>* source = nullptr;

void Detach()
{
    std::lock_guard lock(sourceMutex);
    if (source != nullptr) {
        source->UnregisterSink(&sink);
        source = nullptr;
    }
}

const AdapterRegistration adapter({"menus", [](Runtime& runtime, const Message&) {
    if (!runtime.DataReady()) {
        Detach();
        return;
    }
    std::lock_guard lock(sourceMutex);
    if (source == nullptr) {
        const auto ui = RE::UI::GetSingleton();
        if (ui != nullptr) {
            source = ui->GetEventSource<RE::MenuOpenCloseEvent>();
            source->RegisterSink(&sink);
        }
    }
}, Detach});

}
}
