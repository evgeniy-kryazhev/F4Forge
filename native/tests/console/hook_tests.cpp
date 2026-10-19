#include "../../modules/console/command_hook.h"
#include "../../modules/console/dispatch.h"
#include "../../modules/console/router.h"
#include "../../modules/console/interception.h"
#include <future>
#include <iostream>
#include <thread>

namespace {

using namespace std::chrono_literals;
using f4forge::DataValue;

f4forge::console::CommandHook hook;
f4forge::game_events::Runtime runtime;
std::unique_ptr<f4forge::console::Router> router;
std::unique_ptr<f4forge::interception::Registry> interceptors;
std::atomic<int> originalCalls = 0;
const char* originalText = nullptr;
std::string copiedOriginal;

struct SessionCleanup {
    ~SessionCleanup()
    {
        runtime.Stop();
        interceptors.reset();
        router.reset();
    }
};

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

// A real machine-code entry point, deliberately not inlined or optimized into
// the caller. The detour below uses the same signature as CommonLib's console.
__declspec(noinline) void EngineCommand(const char* text)
{
    originalText = text;
    copiedOriginal = text != nullptr ? text : "";
    originalCalls.fetch_add(1);
}

void OnCommand(const char* text) noexcept
{
    f4forge::console::ForwardCommand(runtime, text, [](const char* input) {
        return f4forge::console::InterceptCommand(interceptors.get(), input, 0x14);
    }, [](const char* input) {
        return router->Execute(input, 0x14);
    }, [](const char* input) {
        hook.Original()(input);
    });
}

void Ready(bool ready)
{
    runtime.Dispatch({static_cast<std::uint32_t>(f4forge::game_events::MessageType::GameDataReady),
        ready ? reinterpret_cast<const void*>(std::uintptr_t{1}) : nullptr, 1});
}

void RoutingThroughRealHook()
{
    f4forge::Channel channel;
    std::vector<std::string> output;
    std::vector<std::string> errors;
    const SessionCleanup cleanup;
    router = std::make_unique<f4forge::console::Router>(channel,
        [&](std::string_view line) { output.emplace_back(line); },
        [](std::string_view alias) { return alias == "help"; });
    runtime.Start(channel, [&](std::string_view error) { errors.emplace_back(error); });
    hook.Install(&EngineCommand, &OnCommand);
    hook.Install(&EngineCommand, &OnCommand);

    const char beforeReady[] = "hello";
    EngineCommand(beforeReady);
    Require(originalCalls == 1 && originalText == beforeReady, "Before readiness, original input was not forwarded exactly once");

    int handled = 0;
    const auto registration = channel.Add("console.command.hello", [&](const DataValue& arguments) {
        ++handled;
        Require(arguments.at("selectedReferenceFormId") == 0x14, "Selected reference was lost by detour");
        return DataValue{{"lines", DataValue::array({"Hello"})}};
    });
    Ready(true);
    EngineCommand("HELLO");
    Require(handled == 1 && originalCalls == 1 && output == std::vector<std::string>{"Hello"}, "Custom command was not consumed exactly once");
    const char vanilla[] = "help \"Fallout\"";
    EngineCommand(vanilla);
    Require(originalCalls == 2 && originalText == vanilla, "Built-in command input was changed");
    const char unknown[] = "unknown \"broken";
    EngineCommand(unknown);
    Require(originalCalls == 3 && originalText == unknown, "Unknown command was swallowed or changed");
    channel.Remove(registration);
    EngineCommand("hello");
    Require(originalCalls == 4 && handled == 1, "Removed command still handled input");
    channel.Add("console.command.hello", [](const DataValue&) -> DataValue {
        throw std::runtime_error("Expected handler failure");
    });
    EngineCommand("hello");
    Require(originalCalls == 4 && output.back() == "F4Forge: Expected handler failure", "Callback exception escaped or triggered original execution");
    Ready(false);
    EngineCommand("hello");
    Require(originalCalls == 5, "Unready data did not disable routing");
    Ready(true);
    runtime.Stop();
    router.reset();
    const char afterStop[] = "hello";
    EngineCommand(afterStop);
    Require(originalCalls == 6 && originalText == afterStop, "Stopped host used released router or failed to call original");

    router = std::make_unique<f4forge::console::Router>(channel,
        [](std::string_view) {}, [](std::string_view) { return false; });
    runtime.Start(channel, [&](std::string_view error) { errors.emplace_back(error); });
    Ready(true);
    hook.Install(&EngineCommand, &OnCommand);
    EngineCommand("hello");
    Require(originalCalls == 6, "Restart duplicated or lost the detour");
    runtime.Stop();
    router.reset();
    Require(errors.empty(), "Console runtime reported an unexpected error");
}

void InterceptionThroughRealHook()
{
    f4forge::Channel channel;
    const SessionCleanup cleanup;
    std::vector<std::string> errors;
    router = std::make_unique<f4forge::console::Router>(channel,
        [](std::string_view) {}, [](std::string_view alias) { return alias == "help"; });
    interceptors = std::make_unique<f4forge::interception::Registry>(channel,
        [&](std::string_view error) { errors.emplace_back(error); });
    runtime.Start(channel, [&](std::string_view error) { errors.emplace_back(error); });
    Ready(true);
    int customCalls = 0;
    int laterCalls = 0;
    std::uint64_t later = 0;
    const auto send = [&](std::string_view method, const DataValue& arguments) {
        const auto& serializer = channel.Serializer();
        return serializer.Deserialize(channel.Call(serializer.Serialize({{"method", method}, {"arguments", arguments}})));
    };
    channel.Add("console.command.hello", [&](const DataValue&) {
        ++customCalls;
        return DataValue{{"lines", DataValue::array()}};
    });
    channel.Add("before.first", [&](const DataValue& input) {
        const auto original = input.at("originalCommand").get<std::string>();
        Require(input.at("selectedReferenceFormId") == 0x14, "Interceptor lost selected reference");
        if (original == "cancel") {
            return DataValue{{"command", "hello"}, {"skip", true}};
        }
        if (original == "nested") {
            EngineCommand("to-engine");
        }
        if (original == "remove") {
            send("interceptors.remove", {{"registration", later}});
        }
        std::string command = "help rewritten";
        if (original == "to-custom") {
            command = "hello";
        } else if (original == "empty") {
            command.clear();
        } else if (original == "nested") {
            command = "nested";
        }
        return DataValue{{"command", command}, {"skip", false}};
    });
    channel.Add("before.later", [&](const DataValue& input) {
        ++laterCalls;
        if (input.at("originalCommand") == "rollback") {
            return DataValue{{"command", std::string("bad\0input", 9)}, {"skip", false}};
        }
        return DataValue{{"command", input.at("command")}, {"skip", false}};
    });
    send("interceptors.register", {{"name", "console.command.executing"}, {"method", "before.first"}});
    later = send("interceptors.register", {{"name", "console.command.executing"}, {"method", "before.later"}}).at("value").get<std::uint64_t>();
    const auto before = originalCalls.load();
    EngineCommand("to-custom");
    Require(customCalls == 1 && originalCalls == before, "Replacement did not route to the custom command");
    EngineCommand("to-engine");
    Require(originalCalls == before + 1 && copiedOriginal == "help rewritten", "Replacement did not reach the original engine function");
    const auto beforeSkip = laterCalls;
    EngineCommand("cancel");
    Require(originalCalls == before + 1 && customCalls == 1 && laterCalls == beforeSkip, "Skip did not terminate all execution");
    EngineCommand("rollback");
    Require(copiedOriginal == "help rewritten" && errors.size() == 1, "Invalid result erased an earlier valid decision");
    EngineCommand("empty");
    Require(copiedOriginal.empty(), "An empty replacement command was rejected");
    const auto beforeNested = originalCalls.load();
    EngineCommand("nested");
    Require(originalCalls == beforeNested + 2 && copiedOriginal == "nested", "Nested interception lost an original call");
    const auto beforeRemove = laterCalls;
    EngineCommand("remove");
    Require(laterCalls == beforeRemove, "Removed snapshot interceptor was still invoked");
    runtime.Stop();
    interceptors.reset();
    const auto beforeStop = originalCalls.load();
    EngineCommand("to-custom");
    Require(originalCalls == beforeStop + 1 && copiedOriginal == "to-custom", "Stopped interception did not preserve the original command");
}

void StopDrainsCallbacks()
{
    f4forge::Channel channel;
    const SessionCleanup cleanup;
    router = std::make_unique<f4forge::console::Router>(channel,
        [](std::string_view) {}, [](std::string_view) { return false; });
    runtime.Start(channel, [](std::string_view) {});
    Ready(true);
    std::promise<void> entered;
    std::promise<void> release;
    const auto released = release.get_future().share();
    channel.Add("console.command.blocked", [&](const DataValue&) {
        entered.set_value();
        released.wait();
        return DataValue{{"lines", DataValue::array()}};
    });
    auto invocation = std::async(std::launch::async, [] { EngineCommand("blocked"); });
    const bool callbackEntered = entered.get_future().wait_for(5s) == std::future_status::ready;
    if (!callbackEntered) {
        release.set_value();
        invocation.get();
        throw std::runtime_error("Hook did not enter callback");
    }
    std::promise<void> stopping;
    auto stopped = std::async(std::launch::async, [&] {
        stopping.set_value();
        runtime.Stop();
    });
    stopping.get_future().wait();
    const bool stopWaited = stopped.wait_for(50ms) == std::future_status::timeout;
    release.set_value();
    invocation.get();
    stopped.get();
    router.reset();
    Require(stopWaited, "Stop released a router with an active callback");
    const auto before = originalCalls.load();
    EngineCommand("blocked");
    Require(originalCalls == before + 1, "Post-stop command did not forward to original");
}

}

int main()
{
    try {
        RoutingThroughRealHook();
        InterceptionThroughRealHook();
        StopDrainsCallbacks();
        std::cout << "Real MinHook console routing, original forwarding, restart and stop barrier passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
