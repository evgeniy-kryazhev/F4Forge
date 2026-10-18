#pragma once

#include "../../game_events/runtime.h"

namespace f4forge::game_events {

inline void MenuChanged(Runtime& runtime, std::string_view menu, bool opening)
{
    runtime.Publish("game.menu.changed", {{"menuName", menu}, {"opening", opening}});
}

}
