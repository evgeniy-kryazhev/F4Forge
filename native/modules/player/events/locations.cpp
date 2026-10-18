#include "locations.h"
#include "location_event.h"
#include "../../game_events/vtable_hook.h"
#include "../../diagnostics/store.h"
#include <REL/Relocation.h>

namespace f4forge::game_events {
namespace {

using Event = RE::TESActorLocationChangeEvent;
using Sink = RE::BSTEventSink<Event>;
using Source = RE::BSTEventSource<Event>;
using Handler = RE::BSEventNotifyControl (*)(Sink*, const Event&, Source*);
VtableHook<Handler> hook;

RE::BSEventNotifyControl OnLocation(Sink* self, const Event& event, Source* source) noexcept
{
    const LocationSnapshot snapshot{
        event.actor ? event.actor->GetFormID() : 0,
        event.oldLocation ? event.oldLocation->GetFormID() : 0,
        event.newLocation ? event.newLocation->GetFormID() : 0};
    return ForwardLocation(Host(), snapshot, [&] {
        return hook.Original()(self, event, source);
    });
}

REL::Relocation<std::uintptr_t> Table()
{
    // Base 16 (offset 0x80) is BSTEventSink<TESActorLocationChangeEvent> in
    // BasicEventHandler; slot 1 is ProcessEvent. The TABLE ADDRESS comes from
    // Address Library, never from a hardcoded executable address.
    return REL::Relocation<std::uintptr_t>(RE::VTABLE::GameScript__BasicEventHandler[16]);
}

Handler ReadSlot()
{
    return reinterpret_cast<Handler>(diagnostics::CheckVtableSlot(Table().address(), 1));
}

void WriteSlot(Handler handler)
{
    auto table = Table();
    table.write_vfunc(1, handler);
    if (ReadSlot() != handler) {
        throw std::runtime_error("Cannot write the player location vtable slot.");
    }
}

void Detach()
{
    hook.Detach(&OnLocation, ReadSlot, WriteSlot);
}

const AdapterRegistration adapter({"locations", [](Runtime& runtime, const Message&) {
    if (runtime.DataReady()) {
        diagnostics::CheckAndInstall("player.location", "vtable", [] {
            hook.Attach(&OnLocation, ReadSlot, WriteSlot);
        });
    } else {
        Detach();
        diagnostics::Global().Hook("player.location", "vtable", "inactive", "Game data is unavailable.");
    }
}, [] {
    Detach();
    diagnostics::Global().Hook("player.location", "vtable", "inactive", "Host stopped.");
}});

}
}
