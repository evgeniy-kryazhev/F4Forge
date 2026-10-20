#include "../../modules/input/events/buttons.h"
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

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

void Translation()
{
    struct Button {
        float value;
        float heldSeconds;

        bool QJustPressed() const
        {
            return value != 0.0F && heldSeconds == 0.0F;
        }

        bool QReleased() const
        {
            return value == 0.0F && heldSeconds > 0.0F;
        }
    };
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
    Require(f4forge::input::ButtonPhase(Button{1.0F, 0.0F}) == true, "Press lost");
    Require(f4forge::input::ButtonPhase(Button{0.2F, 0.0F}) == true, "Analog trigger press lost");
    Require(!f4forge::input::ButtonPhase(Button{1.0F, 1.5F}).has_value(), "Holding repeated press");
    Require(f4forge::input::ButtonPhase(Button{0.0F, 1.5F}) == false, "Release lost");
    Require(!f4forge::input::ButtonPhase(Button{0.0F, 0.0F}).has_value(), "Empty state released");
}

void Forwarding()
{
    f4forge::Channel channel;
    Runtime runtime;
    int failures = 0;
    int originalCalls = 0;
    runtime.Start(channel, [&](std::string_view) {
        ++failures;
    });
    std::vector<ButtonSnapshot> queue{{0, 30, "Action Ω", 0.0F, true}, {1, 256, "", 2.5F, false}};
    std::vector<DataValue> received;
    const auto subscribe = [&](const char* name) {
        channel.Events().Subscribe(name, [&](const DataValue& payload) {
            Require(originalCalls > 0, "Published before the original receiver");
            received.push_back(payload);
        });
    };
    subscribe("game.input.button.pressed");
    subscribe("game.input.button.released");
    f4forge::input::ForwardButtons(runtime, [&] {
        return queue;
    }, [&] {
        ++originalCalls;
        queue[0].userEvent = "changed";
    });
    Require(originalCalls == 1 && received.size() == 2, "Incorrect dispatch count");
    Require(received[0] == DataValue({{"device", 0}, {"keyCode", 30}, {"userEvent", "Action Ω"}, {"heldSeconds", 0.0F}}),
        "Snapshot was borrowed or wire contract changed");
    Require(received[1].at("heldSeconds") == 2.5F && received[1].at("userEvent") == "", "Release payload changed");
    Require(queue.size() == 2 && queue[0].keyCode == 30 && queue[1].keyCode == 256, "Input queue changed");
    f4forge::input::ForwardButtons(runtime, []() -> std::vector<ButtonSnapshot> {
        throw std::runtime_error("Snapshot failure");
    }, [&] {
        ++originalCalls;
    });
    Require(originalCalls == 2 && failures == 1, "Snapshot failure prevented original processing");
    f4forge::input::ForwardButtons(runtime, [] {
        return std::vector<ButtonSnapshot>{};
    }, [&] {
        ++originalCalls;
    });
    Require(originalCalls == 3 && received.size() == 2, "Empty queue produced events");
    runtime.Stop();
    bool capturedAfterStop = false;
    f4forge::input::ForwardButtons(runtime, [&] {
        capturedAfterStop = true;
        return queue;
    }, [&] {
        ++originalCalls;
    });
    Require(!capturedAfterStop && originalCalls == 4 && received.size() == 2, "Stopped hook did not forward transparently");
}

void QueueSnapshots()
{
    struct Event {
        Event* next;
        std::optional<ButtonSnapshot> button;
        int handled;
    };
    Event released{nullptr, ButtonSnapshot{1, 256, "", 1.0F, false}, 2};
    Event motion{&released, std::nullopt, 1};
    Event pressed{&motion, ButtonSnapshot{0, 30, "Jump", 0.0F, true}, 0};
    const auto decode = [](const Event& event) {
        return event.button;
    };
    const auto notifications = f4forge::input::SnapshotButtons(&pressed, decode);
    pressed.button->userEvent = "changed";
    Require(notifications.size() == 2 && notifications[0].pressed && !notifications[1].pressed,
        "Queue order changed or non-button event included");
    Require(notifications[0].userEvent == "Jump", "Snapshot retained borrowed button data");
    Require(pressed.next == &motion && motion.next == &released && released.next == nullptr,
        "Queue links changed");
    Require(pressed.handled == 0 && motion.handled == 1 && released.handled == 2,
        "Queue handled flags changed");
    Require(f4forge::input::SnapshotButtons(static_cast<const Event*>(nullptr), decode).empty(),
        "Null queue produced a notification");
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
    channel.Events().Subscribe("game.input.button.pressed", [&](const DataValue& payload) {
        // SDK subscriptions serialize at this boundary; native subscribers do not.
        channel.Serializer().Serialize(payload);
        ++deliveries;
    });
    runtime.Start(channel, [&](std::string_view) {
        ++failures;
    });
    f4forge::input::ForwardButtons(runtime, [] {
        return std::vector<ButtonSnapshot>{{0, 30, "", 0.0F, true}, {0, 31, "", 0.0F, true}};
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
    readiness.Observe({static_cast<unsigned>(MessageType::GameLoaded), nullptr, 0}, attach);
    Require(attempts == 0, "Attached before input loaded");
    readiness.Observe({static_cast<unsigned>(MessageType::InputLoaded), nullptr, 0}, attach);
    Require(attempts == 1 && !installed, "Missing singleton was not tolerated");
    singletonAvailable = true;
    readiness.Observe({static_cast<unsigned>(MessageType::GameDataReady), nullptr, 0}, attach);
    readiness.Observe({static_cast<unsigned>(MessageType::InputLoaded), nullptr, 0}, attach);
    Require(installed && installations == 1, "Attachment was not retried or was duplicated");
    readiness.Reset();
    const auto previousAttempts = attempts;
    readiness.Observe({static_cast<unsigned>(MessageType::GameLoaded), nullptr, 0}, attach);
    Require(attempts == previousAttempts, "Readiness survived shutdown");
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
    channel.Events().Subscribe("game.input.button.pressed", [&](const DataValue&) {
        entered.set_value();
        released.wait();
    });
    auto publisher = std::async(std::launch::async, [&] {
        f4forge::input::ForwardButtons(runtime, [] {
            return std::vector<ButtonSnapshot>{{0, 30, "", 0.0F, true}};
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
        Forwarding();
        QueueSnapshots();
        PublicationFailure();
        AttachmentLifecycle();
        StopDuringCallback();
        HookChaining();
        std::cout << "Input tests passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
