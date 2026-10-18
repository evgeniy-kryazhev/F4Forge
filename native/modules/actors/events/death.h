#pragma once

#include "../../game_events/runtime.h"
#include "../../game_events/references.h"

namespace f4forge::game_events {

inline void ActorDied(Runtime& runtime, std::uint32_t actor, std::uint32_t killer, bool dead)
{
    if (dead && actor != 0) {
        runtime.Publish("game.actor.died", {{"actorFormId", actor}, {"killerFormId", killer}, {"isPlayer", actor == PlayerFormId}});
    }
}

}
