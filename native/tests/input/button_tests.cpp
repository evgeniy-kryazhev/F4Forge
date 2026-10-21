#include "../../modules/input/events/event_delivery.h"
#include "../../modules/game_events/vtable_hook.h"
#include <chrono>
#include <future>
#include <iostream>
#include <thread>

namespace {

using f4forge::DataValue;
using f4forge::game_events::MessageType;
using f4forge::game_events::Runtime;
using f4forge::input::ButtonSnapshot;
using f4forge::input::KeyPhase;

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

void Translation()
{
    const auto gamepad = [](std::uint32_t mask) -> std::uint32_t {
        return mask == 0x1000 ? 276U : 282U;
    };
    Require(f4forge::input::KeyCode(0, 0, gamepad) == 0, "Keyboard code zero lost");
    Require(f4forge::input::KeyCode(0, 255, gamepad) == 255, "Keyboard upper bound lost");
    Require(!f4forge::input::KeyCode(0, 256, gamepad), "Invalid keyboard code accepted");
    Require(f4forge::input::KeyCode(1, 0, gamepad) == 256, "Mouse offset incorrect");
    Require(f4forge::input::KeyCode(1, 7, gamepad) == 263, "Last mouse button lost");
    Require(!f4forge::input::KeyCode(1, 8, gamepad), "Mouse wheel accepted");
    Require(f4forge::input::KeyCode(2, 0x1000, gamepad) == 276, "Gamepad converter ignored");
    Require(!f4forge::input::KeyCode(2, 0, gamepad), "Unknown gamepad button accepted");
    Require(!f4forge::input::KeyCode(-1, 1, gamepad), "Unknown device accepted");
    Require(!f4forge::input::KeyCode(4, 1, gamepad), "Virtual keyboard accepted");

}

void Forwarding()
{
    f4forge::Channel channel;
    Runtime runtime;
    int failures = 0;
    int calls = 0;
    runtime.Start(channel, [&](std::string_view) {
        ++failures;
    });
    f4forge::input::CaptureAndForward(runtime, []() -> std::vector<ButtonSnapshot> {
        throw std::runtime_error("Capture failure");
    }, [&] {
        ++calls;
    });
    Require(calls == 1 && failures == 1, "Capture failure repeated original batch");
    f4forge::input::ForwardDevice(runtime, [&] {
        ++calls;
    }, [](auto&&) {
        throw std::runtime_error("Failure before original");
    });
    Require(calls == 2 && failures == 2, "Setup failure skipped original batch");
    f4forge::input::ForwardDevice(runtime, [&] {
        ++calls;
        throw std::runtime_error("Original batch failure");
    }, [&](auto&& forward) {
        forward();
    });
    Require(calls == 3 && failures == 3, "Failing original batch was called twice or escaped");
    runtime.Stop();
    bool observed = false;
    f4forge::input::ForwardDevice(runtime, [&] {
        ++calls;
    }, [&](auto&&) {
        observed = true;
    });
    Require(calls == 4 && !observed, "Stopped host did not forward transparently");
}

void CopiedPayloads()
{
    f4forge::Channel channel;
    Runtime runtime;
    runtime.Start(channel, [](std::string_view) {});
    std::vector<ButtonSnapshot> source{{0, 87, "Forward Ω", 0, KeyPhase::Down}};
    int originalCalls = 0;
    int deliveries = 0;
    channel.Events().Subscribe("game.input.key.down", [&](const DataValue& payload) {
        Require(originalCalls == 1, "Published before original processing");
        Require(payload.at("userEvent") == "Forward Ω", "Payload retained engine memory");
        ++deliveries;
    });
    f4forge::input::CaptureAndForward(runtime, [&] {
        return source;
    }, [&] {
        ++originalCalls;
        source.clear();
    });
    Require(deliveries == 1, "Copied notification missing");
    runtime.Stop();
}

void ReceiverRouting()
{
    for (int device = -1; device < 5; ++device) {
        const bool camera = f4forge::input::ReceivesDevice(true, device);
        const bool controls = f4forge::input::ReceivesDevice(false, device);
        Require(!(camera && controls), "Both receivers accepted the same device");
        Require((camera || controls) == (device >= 0 && device <= 2), "Device coverage changed");
    }
}

class FailingSerializer final : public f4forge::IDataSerializer {
public:
    std::string_view Format() const noexcept override
    {
        return "test";
    }

    std::string Serialize(const DataValue& value) const override
    {
        if (value.at("keyCode") == 30) {
            throw std::runtime_error("Cannot serialize this button");
        }
        return value.dump();
    }

    DataValue Deserialize(std::string_view bytes) const override
    {
        return DataValue::parse(bytes);
    }
};

void PublicationFailure()
{
    f4forge::Channel channel(std::make_shared<FailingSerializer>());
    Runtime runtime;
    int failures = 0;
    int calls = 0;
    int deliveries = 0;
    channel.Events().SetErrorSink([&](std::string_view) {
        ++failures;
    });
    channel.Events().Subscribe("game.input.key.down", [&](const DataValue& payload) {
        Require(calls == 1, "Published before original batch returned");
        // SDK subscriptions serialize at this boundary; native subscribers do not.
        channel.Serializer().Serialize(payload);
        ++deliveries;
    });
    runtime.Start(channel, [&](std::string_view) {
        ++failures;
    });
    f4forge::input::CaptureAndForward(runtime, [] {
        return std::vector<ButtonSnapshot>{{0, 30, "", 0.0F, KeyPhase::Down}, {0, 31, "", 0.0F, KeyPhase::Down}};
    }, [&] {
        ++calls;
    });
    Require(calls == 1 && failures == 1 && deliveries == 1, "Failed publication discarded later buttons");
    runtime.Stop();
}

void AttachmentLifecycle()
{
    f4forge::input::InputReadiness readiness;
    bool singletonAvailable = false;
    bool installed = false;
    int attempts = 0;
    int installations = 0;
    const auto attach = [&] {
        ++attempts;
        if (singletonAvailable && !installed) {
            installed = true;
            ++installations;
        }
    };
    readiness.Observe({static_cast<unsigned>(MessageType::GameDataReady), nullptr, 0}, attach);
    Require(attempts == 0, "Attached before input loaded");
    readiness.Observe({static_cast<unsigned>(MessageType::InputLoaded), nullptr, 0}, attach);
    Require(attempts == 1 && !installed, "Missing singleton was not tolerated");
    singletonAvailable = true;
    readiness.Observe({static_cast<unsigned>(MessageType::GameDataReady), nullptr, 0}, attach);
    readiness.Observe({static_cast<unsigned>(MessageType::InputLoaded), nullptr, 0}, attach);
    Require(installed && installations == 1, "Attachment was not retried or was duplicated");
    readiness.Reset();
    const auto previousAttempts = attempts;
    readiness.Observe({static_cast<unsigned>(MessageType::GameDataReady), nullptr, 0}, attach);
    Require(attempts == previousAttempts, "Readiness survived shutdown");
}

void AttachmentWithoutInputLoaded()
{
    int readyMarker = 1;
    const std::vector<f4forge::game_events::Message> startupMessages{
        {static_cast<unsigned>(MessageType::GameLoaded), nullptr, 0},
        {static_cast<unsigned>(MessageType::GameDataReady), &readyMarker, 1},
        {static_cast<unsigned>(MessageType::PostLoadGame), &readyMarker, 1},
        {static_cast<unsigned>(MessageType::NewGame), nullptr, 0}};
    for (const auto& startup : startupMessages) {
        f4forge::input::InputReadiness readiness;
        int attempts = 0;
        const auto attach = [&] {
            ++attempts;
        };
        readiness.Observe({static_cast<unsigned>(MessageType::GameDataReady), nullptr, 1}, attach);
        readiness.Observe({static_cast<unsigned>(MessageType::PostLoadGame), nullptr, 1}, attach);
        Require(attempts == 0, "Failed load enabled input hooks");
        readiness.Observe(startup, attach);
        Require(attempts == 1, "InputLoaded was required despite a ready game");
        readiness.Observe({static_cast<unsigned>(MessageType::PostSaveGame), nullptr, 0}, attach);
        Require(attempts == 2, "Missing receiver cannot retry after game readiness");
        readiness.Reset();
        readiness.Observe({static_cast<unsigned>(MessageType::PostSaveGame), nullptr, 0}, attach);
        Require(attempts == 2, "Reset retained game readiness");
    }
}

void StopDuringCallback()
{
    using namespace std::chrono_literals;
    f4forge::Channel channel;
    Runtime runtime;
    runtime.Start(channel, [](std::string_view) {});
    std::promise<void> entered;
    std::promise<void> release;
    auto released = release.get_future().share();
    channel.Events().Subscribe("game.input.key.down", [&](const DataValue&) {
        entered.set_value();
        released.wait();
    });
    auto publisher = std::async(std::launch::async, [&] {
        f4forge::input::CaptureAndForward(runtime, [] {
            return std::vector<ButtonSnapshot>{{0, 30, "", 0.0F, KeyPhase::Down}};
        }, [] {});
    });
    entered.get_future().wait();
    std::promise<void> stopping;
    auto stopped = std::async(std::launch::async, [&] {
        stopping.set_value();
        runtime.Stop();
    });
    stopping.get_future().wait();
    const auto stopWasWaiting = stopped.wait_for(30ms) == std::future_status::timeout;
    release.set_value();
    publisher.get();
    stopped.get();
    Require(stopWasWaiting, "Shutdown did not drain the active input callback");
}

int OriginalHandler()
{
    return 1;
}

int InputHandler()
{
    return 2;
}

int LaterHandler()
{
    return 3;
}

void HookChaining()
{
    using Handler = int (*)();
    Handler slot = &OriginalHandler;
    f4forge::game_events::VtableHook<Handler> hook;
    const auto read = [&] {
        return slot;
    };
    const auto write = [&](Handler value) {
        slot = value;
    };
    hook.Attach(&InputHandler, read, write);
    hook.Attach(&InputHandler, read, write);
    Require(hook.Original() == &OriginalHandler && slot == &InputHandler, "Repeated attach changed original");
    hook.Detach(&InputHandler, read, write);
    Require(slot == &OriginalHandler, "Detach did not restore original");
    hook.Attach(&InputHandler, read, write);
    slot = &LaterHandler;
    hook.Detach(&InputHandler, read, write);
    hook.Attach(&InputHandler, read, write);
    Require(slot == &LaterHandler && hook.Original() == &OriginalHandler, "Later plugin's chain was overwritten");
}

}

int main()
{
    try {
        Translation();
        ReceiverRouting();
        CopiedPayloads();
        Forwarding();
        PublicationFailure();
        AttachmentLifecycle();
        AttachmentWithoutInputLoaded();
        StopDuringCallback();
        HookChaining();
        std::cout << "Input tests passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
