#include "../../modules/ui/service.h"
#include <iostream>
#include <stdexcept>

namespace {

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

f4forge::DataValue Query(f4forge::Channel& channel)
{
    const auto& serializer = channel.Serializer();
    const auto request = serializer.Serialize({{"method", "ui.is_pause_menu_open"}, {"arguments", nullptr}});
    return serializer.Deserialize(channel.Call(request));
}

void StateAndLifetime()
{
    f4forge::Channel channel;
    std::optional<bool> state = false;
    bool gameThread = true;
    int reads = 0;
    const f4forge::ui::Backend backend{
        [&] { return gameThread; },
        [&] {
            ++reads;
            return state;
        }};

    {
        f4forge::ui::Service service(channel, backend);
        Require(Query(channel).at("value") == false, "Closed pause menu must return false");
        state = true;
        Require(Query(channel).at("value") == true, "Open pause menu must return true");
        state = false;
        Require(Query(channel).at("value") == false, "Pause menu state must not be cached");
        state = std::nullopt;
        Require(Query(channel).at("value") == false, "Unavailable UI must return false");

        gameThread = false;
        const auto failure = Query(channel);
        Require(failure.at("success") == false, "Off-thread query must fail");
        Require(failure.at("error").at("code") == "handler_failed", "Expected standard channel error");
        const auto message = failure.at("error").at("message").get<std::string>();
        Require(message.find("game thread") != std::string::npos, "Error must explain thread requirement");
        Require(reads == 4, "Off-thread query must not access UI");
    }

    const auto stopped = Query(channel);
    Require(stopped.at("error").at("code") == "method_not_found", "Stopped service must unregister query");
    gameThread = true;
    state = true;
    {
        f4forge::ui::Service restarted(channel, backend);
        Require(Query(channel).at("value") == true, "Service must support restart");
    }
}

void BackendFailures()
{
    f4forge::Channel channel;
    f4forge::ui::Service service(channel, {
        [] { return true; },
        []() -> std::optional<bool> {
            throw std::runtime_error("UI backend failure");
        }});
    const auto response = Query(channel);
    Require(response.at("success") == false, "Backend exception must become a channel error");
    Require(response.at("error").at("message") == "UI backend failure", "Backend error must be preserved");
}

}

int main()
{
    try {
        StateAndLifetime();
        BackendFailures();
        std::cout << "UI tests passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
