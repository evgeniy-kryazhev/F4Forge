#include "../modules/actors/events/death.h"
#include "../modules/player/events/cells.h"
#include "../modules/player/events/locations.h"
#include "../modules/ui/events/menus.h"
#include "../modules/game_events/vtable_hook.h"
#include <chrono>
#include <future>
#include <iostream>
#include <thread>

namespace {

using namespace std::chrono_literals;
using f4forge::DataValue;
using f4forge::game_events::MessageType;

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

std::atomic<int> stops = 0;
const f4forge::game_events::AdapterRegistration failingAdapter({
    "test.failure", [](auto&, const auto& message) {
        if (message.Is(MessageType::GameLoaded)) {
            throw std::runtime_error("Test adapter failure");
        }
    }, [] {
        ++stops;
    }});

void Lifecycle()
{
    f4forge::Channel channel;
    f4forge::game_events::Runtime runtime;
    std::vector<std::string> errors;
    std::vector<std::pair<std::string, DataValue>> notifications;
    for (const auto* name : {"game.runtime.loaded", "game.data.ready", "game.new", "game.save.loading",
             "game.save.loaded", "game.save.saving", "game.save.saved"}) {
        channel.Events().Subscribe(name, [&, name](const DataValue& value) {
            notifications.emplace_back(name, value);
        });
    }
    runtime.Start(channel, [&](std::string_view error) { errors.emplace_back(error); });
    Require(notifications.empty(), "Startup replayed a game event");
    const auto send = [&](MessageType type, const void* data = nullptr, std::uint32_t size = 0) {
        runtime.Dispatch({static_cast<std::uint32_t>(type), data, size});
    };
    const auto* booleanTrue = reinterpret_cast<const void*>(std::uintptr_t{1});
    send(MessageType::GameLoaded);
    send(MessageType::GameLoaded);
    Require(notifications.size() == 1 && errors.size() == 2, "Runtime event duplicated or adapter error blocked delivery");
    send(MessageType::GameDataReady, booleanTrue, 1);
    Require(runtime.DataReady() && notifications.back().second.at("ready") == true, "Pointer-valued true was not decoded");

    char saveName[] = "Save Ω";
    send(MessageType::PreLoadGame, saveName, sizeof(saveName) - 1);
    saveName[0] = 'X';
    send(MessageType::PostLoadGame, booleanTrue, 1);
    Require(notifications.back().first == "game.save.loaded", "Missing saved-game load notification");
    Require(notifications.back().second == DataValue({{"saveName", "Save Ω"}, {"success", true}}), "Save name was borrowed or load result lost");
    send(MessageType::PostLoadGame, nullptr, 1);
    Require(notifications.back().second == DataValue({{"saveName", nullptr}, {"success", false}}), "Failed/orphan load used stale state");
    send(MessageType::PreSaveGame, "Save", 4);
    Require(notifications.back().first == "game.save.saving", "Missing pre-save notification");
    send(MessageType::PostSaveGame, "Save\0ignored", 12);
    Require(notifications.back().second == DataValue({{"saveName", "Save"}}), "Save result invented success or retained terminator");
    send(MessageType::PreLoadGame, "Old", 3);
    send(MessageType::NewGame);
    Require(notifications.back().first == "game.new" && notifications.back().second.is_object(), "New-game pointer was exposed");
    send(MessageType::PostLoadGame, booleanTrue, 1);
    Require(notifications.back().second.at("saveName").is_null(), "New game did not reset pending save");
    send(MessageType::GameDataReady, nullptr, 1);
    Require(!runtime.DataReady() && notifications.back().second.at("ready") == false, "Pointer-valued false was not decoded");
    send(MessageType::PreLoadGame, "X", f4forge::Channel::MaxMessageBytes + 1);
    Require(errors.size() == 3, "Invalid input was not isolated at adapter boundary");
    const auto count = notifications.size();
    runtime.Stop();
    send(MessageType::GameLoaded);
    runtime.Invoke([&] { throw std::runtime_error("Inactive callback executed"); });
    Require(notifications.size() == count, "Stopped listener published an event");
    const auto stopCount = stops.load();
    runtime.Stop();
    Require(stops == stopCount, "Repeated shutdown detached adapters twice");
    runtime.Start(channel, {});
    send(MessageType::GameLoaded);
    Require(notifications.size() == count + 1, "Restart retained runtime-loaded state");
    runtime.Stop();
}

void NotificationsAndHook()
{
    using namespace f4forge::game_events;
    f4forge::Channel channel;
    Runtime runtime;
    runtime.Start(channel, {});
    std::vector<DataValue> deaths;
    channel.Events().Subscribe("game.actor.died", [&](const auto& data) { deaths.push_back(data); });
    runtime.Invoke([&] {
        ActorDied(runtime, PlayerFormId, 0, false);
        ActorDied(runtime, 0, 123, true);
        ActorDied(runtime, PlayerFormId, 0, true);
        ActorDied(runtime, 123, PlayerFormId, true);
    });
    Require(deaths.size() == 2 && deaths[0].at("isPlayer") == true && deaths[0].at("killerFormId") == 0,
        "Death filtering or null killer mapping failed");
    Require(deaths[1].at("actorFormId") == 123 && deaths[1].at("isPlayer") == false, "NPC death was filtered");

    CellChanges changes;
    Require(!changes.Enter(123, 1, true) && !changes.Enter(PlayerFormId, 1, false), "Non-player or leave event became a cell transition");
    Require(!changes.Enter(PlayerFormId, 0, true), "Null cell became a transition");
    Require(changes.Enter(PlayerFormId, 1, true)->at("previousCellFormId") == 0, "Initial cell baseline invalid");
    Require(!changes.Enter(PlayerFormId, 1, true), "Duplicate cell event delivered");
    Require(changes.Enter(PlayerFormId, 2, true)->at("previousCellFormId") == 1, "Previous cell not tracked");
    changes.Reset();
    Require(changes.Enter(PlayerFormId, 3, true)->at("previousCellFormId") == 0, "Cell tracker crossed game sessions");

    std::vector<DataValue> locations;
    std::vector<int> observedOriginalCalls;
    int originalCalls = 0;
    channel.Events().Subscribe("game.player.location.changed", [&](const auto& data) {
        observedOriginalCalls.push_back(originalCalls);
        locations.push_back(data);
    });
    const auto original = [&] {
        ++originalCalls;
        return 17;
    };
    Require(ForwardLocation(runtime, {PlayerFormId, 100, 200}, original) == 17, "Hook changed the original result");
    Require(originalCalls == 1 && observedOriginalCalls == std::vector<int>{1}
        && locations.size() == 1 && locations[0].at("previousLocationFormId") == 100,
        "Hook lost the original call or location payload");
    ForwardLocation(runtime, {123, 100, 200}, original);
    ForwardLocation(runtime, {PlayerFormId, 200, 200}, original);
    Require(locations.size() == 1 && originalCalls == 3, "NPC/duplicate filtering changed original behavior");
    ForwardLocation(runtime, {0, 100, 200}, original);
    ForwardLocation(runtime, {PlayerFormId, 200, 0}, original);
    Require(locations.size() == 2 && locations.back().at("newLocationFormId") == 0,
        "Null actor filtering or leaving a named location failed");
    std::string menu;
    channel.Events().Subscribe("game.menu.changed", [&](const auto& data) { menu = data.at("menuName").template get<std::string>(); });
    runtime.Invoke([&] { MenuChanged(runtime, "PipboyMenu Ω", true); });
    Require(menu == "PipboyMenu Ω", "Menu text was not copied");
    runtime.Stop();
    ForwardLocation(runtime, {PlayerFormId, 0, 200}, original);
    Require(originalCalls == 6 && locations.size() == 2, "Stopped hook lost original behavior or still published");
}

void StopDrainsCallbacks()
{
    f4forge::Channel channel;
    f4forge::game_events::Runtime runtime;
    runtime.Start(channel, {});
    std::promise<void> entered;
    std::promise<void> release;
    auto released = release.get_future().share();
    auto callback = std::async(std::launch::async, [&] {
        runtime.Invoke([&] {
            entered.set_value();
            released.wait();
        });
    });
    const auto enteredInTime = entered.get_future().wait_for(5s) == std::future_status::ready;
    auto stopping = std::async(std::launch::async, [&] { runtime.Stop(); });
    const auto waited = stopping.wait_for(100ms) == std::future_status::timeout;
    release.set_value();
    callback.get();
    stopping.get();
    Require(enteredInTime && waited, "Stop did not drain an active engine callback");
}

void HookRegistration()
{
    using Handler = int (*)(int);
    const Handler original = [](int value) { return value + 1; };
    const Handler replacement = [](int value) { return value + 2; };
    const Handler otherPlugin = [](int value) { return value + 3; };
    Handler slot = original;
    int writes = 0;
    f4forge::game_events::VtableHook<Handler> hook;
    const auto read = [&] { return slot; };
    const auto write = [&](Handler handler) {
        Require(hook.Original() == original, "Original handler was not initialized before installation");
        slot = handler;
        ++writes;
    };
    hook.Detach(replacement, read, write);
    hook.Attach(replacement, read, write);
    hook.Attach(replacement, read, write);
    Require(slot == replacement && writes == 1 && hook.Original()(41) == 42, "Hook reinstalled or lost original");
    hook.Detach(replacement, read, write);
    hook.Detach(replacement, read, write);
    Require(slot == original && writes == 2, "Hook did not restore its original exactly once");
    hook.Attach(replacement, read, write);
    slot = otherPlugin;
    hook.Detach(replacement, read, write);
    hook.Attach(replacement, read, write);
    Require(slot == otherPlugin && hook.Original() == original && writes == 3, "Hook overwrote a later plugin's chain");
}

}

int main()
{
    try {
        Lifecycle();
        NotificationsAndHook();
        StopDrainsCallbacks();
        HookRegistration();
        std::cout << "Game lifecycle, payloads, hook forwarding, filtering and shutdown passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
