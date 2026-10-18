#include "../../game_events/runtime.h"
#include <optional>

namespace f4forge::game_events {
namespace {

std::mutex stateMutex;
std::optional<std::string> loadingSave;

DataValue SaveName(const Message& message)
{
    if (message.data == nullptr || message.size == 0) {
        return nullptr;
    }
    if (message.size > Channel::MaxMessageBytes) {
        throw std::length_error("F4SE save name exceeds the message limit.");
    }
    auto name = std::string_view(static_cast<const char*>(message.data), message.size);
    if (const auto terminator = name.find('\0'); terminator != std::string_view::npos) {
        name = name.substr(0, terminator);
    }
    return std::string(name);
}

void OnMessage(Runtime& runtime, const Message& message)
{
    std::string_view name;
    DataValue data = DataValue::object();
    {
        std::lock_guard lock(stateMutex);
        if (message.Is(MessageType::PreLoadGame)) {
            data["saveName"] = SaveName(message);
            loadingSave = data["saveName"].is_null() ? std::nullopt : std::optional(data["saveName"].get<std::string>());
            name = "game.save.loading";
        } else if (message.Is(MessageType::PostLoadGame)) {
            data["saveName"] = loadingSave ? DataValue(*loadingSave) : DataValue(nullptr);
            data["success"] = message.data != nullptr;
            loadingSave.reset();
            name = "game.save.loaded";
        } else if (message.Is(MessageType::PreSaveGame)) {
            data["saveName"] = SaveName(message);
            name = "game.save.saving";
        } else if (message.Is(MessageType::PostSaveGame)) {
            data["saveName"] = SaveName(message);
            name = "game.save.saved";
        } else if (message.Is(MessageType::NewGame)
            || (message.Is(MessageType::GameDataReady) && message.data == nullptr)) {
            loadingSave.reset();
        }
    }
    if (!name.empty()) {
        runtime.Publish(name, data);
    }
}

const AdapterRegistration adapter({"lifecycle.saves", OnMessage, [] {
    std::lock_guard lock(stateMutex);
    loadingSave.reset();
}, 50}); // Reset save correlation before general new-game/readiness notifications.

}
}
