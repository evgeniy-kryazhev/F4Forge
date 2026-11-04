#pragma once

#include "../game_events/runtime.h"
#include <F4SE/F4SE.h>
#include <RE/Fallout.h>

namespace f4forge::extensions
{
inline void RequireGameThread()
{
    const auto* main = RE::Main::GetSingleton();
    if (main == nullptr || main->threadID != REX::W32::GetCurrentThreadId())
    {
        throw std::runtime_error("This operation requires the game thread.");
    }
}

inline DataValue Call(Channel& channel, const std::string& method, const DataValue& arguments)
{
    const DataValue request{{"method", method}, {"arguments", arguments}};
    const auto response = channel.Serializer().Deserialize(channel.Call(channel.Serializer().Serialize(request)));
    if (!response.at("success").get<bool>())
    {
        throw std::runtime_error(response.at("error").at("message").get<std::string>());
    }
    return response.at("value");
}

inline void Queue(std::function<void()> action)
{
    const auto* tasks = F4SE::GetTaskInterface();
    if (tasks == nullptr)
    {
        throw std::runtime_error("F4SE task interface is unavailable.");
    }
    tasks->AddTask([action = std::move(action)] {
        game_events::Host().Invoke(action);
    });
}
} // namespace f4forge::extensions
