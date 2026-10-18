#include "../../game_events/runtime.h"

namespace f4forge::game_events {
namespace {

std::mutex stateMutex;
bool runtimeLoaded = false;

void OnMessage(Runtime& runtime, const Message& message)
{
    if (message.Is(MessageType::GameLoaded)) {
        {
            std::lock_guard lock(stateMutex);
            if (runtimeLoaded) {
                return;
            }
            runtimeLoaded = true;
        }
        runtime.Publish("game.runtime.loaded", DataValue::object());
    } else if (message.Is(MessageType::NewGame)) {
        runtime.Publish("game.new", DataValue::object());
    } else if (message.Is(MessageType::GameDataReady)) {
        runtime.Publish("game.data.ready", {{"ready", message.data != nullptr}});
    }
}

const AdapterRegistration adapter({"lifecycle.game", OnMessage, [] {
    std::lock_guard lock(stateMutex);
    runtimeLoaded = false;
}, 100}); // Attach sources and reset subsystem state before notifying mods.

}
}
