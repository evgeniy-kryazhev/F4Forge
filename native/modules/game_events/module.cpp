#include "components.h"
#include "runtime.h"
#include <F4SE/F4SE.h>
#include <REX/REX.h>

namespace {

using f4forge::game_events::MessageType;
static_assert(static_cast<unsigned>(MessageType::PreLoadGame) == F4SE::MessagingInterface::kPreLoadGame);
static_assert(static_cast<unsigned>(MessageType::PostLoadGame) == F4SE::MessagingInterface::kPostLoadGame);
static_assert(static_cast<unsigned>(MessageType::PreSaveGame) == F4SE::MessagingInterface::kPreSaveGame);
static_assert(static_cast<unsigned>(MessageType::PostSaveGame) == F4SE::MessagingInterface::kPostSaveGame);
static_assert(static_cast<unsigned>(MessageType::InputLoaded) == F4SE::MessagingInterface::kInputLoaded);
static_assert(static_cast<unsigned>(MessageType::NewGame) == F4SE::MessagingInterface::kNewGame);
static_assert(static_cast<unsigned>(MessageType::GameLoaded) == F4SE::MessagingInterface::kGameLoaded);
static_assert(static_cast<unsigned>(MessageType::GameDataReady) == F4SE::MessagingInterface::kGameDataReady);

bool listenerRegistered = false;

void OnMessage(F4SE::MessagingInterface::Message* message) noexcept
{
    if (message != nullptr) {
        f4forge::game_events::Host().Dispatch({message->type, message->data, message->dataLen});
    }
}

const f4forge::ComponentRegistration component({
    "game_events",
    [](f4forge::Channel& channel, const std::filesystem::path&) {
        f4forge::game_events::Host().Start(channel, [](std::string_view message) {
            REX::ERROR("Game events: {}", message);
        });
        if (!listenerRegistered) {
            const auto* messaging = F4SE::GetMessagingInterface();
            if (messaging == nullptr || !messaging->RegisterListener(OnMessage)) {
                throw std::runtime_error("Cannot register the F4SE game event listener.");
            }
            listenerRegistered = true;
        }
    },
    [] {
        f4forge::game_events::Host().Stop();
    }
});

}
