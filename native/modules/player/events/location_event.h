#pragma once

#include <RE/Fallout.h>
#include <cstddef>

namespace RE {

// CommonLib declares this event but does not define its payload. Kept here rather
// than modifying the submodule. Matches the engine's BasicEventHandler contract.
// Reference: Neanka/f4se, f4se/f4seee/f4seeeEvents.h (TESActorLocationChangeEvent).
class TESActorLocationChangeEvent {
public:
    Actor* actor;
    BGSLocation* oldLocation;
    BGSLocation* newLocation;
};

static_assert(sizeof(TESActorLocationChangeEvent) == 0x18);
static_assert(offsetof(TESActorLocationChangeEvent, actor) == 0x00);
static_assert(offsetof(TESActorLocationChangeEvent, oldLocation) == 0x08);
static_assert(offsetof(TESActorLocationChangeEvent, newLocation) == 0x10);

}
