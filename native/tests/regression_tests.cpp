#include "channel.h"
#include "../modules/game_events/runtime.h"
#include "../modules/papyrus_registration/integer.h"
#include "../modules/prisma_ui/cleanup.h"
#include <functional>
#include <iostream>
#include <limits>
#include <stdexcept>
#include <vector>

namespace
{
void Require(bool condition, const char* message)
{
    if (!condition)
    {
        throw std::runtime_error(message);
    }
}

class StrictSerializer final : public f4forge::IDataSerializer
{
public:
    std::string_view Format() const noexcept override { return "strict-json"; }
    std::string Serialize(const f4forge::DataValue& value) const override { return value.dump(); }
    f4forge::DataValue Deserialize(std::string_view bytes) const override
    {
        return f4forge::DataValue::parse(bytes);
    }
};

void CheckErrorTruncation(std::shared_ptr<const f4forge::IDataSerializer> serializer)
{
    f4forge::Channel channel(std::move(serializer));
    std::string failure;
    channel.Add("fail", [&failure](const auto&) -> f4forge::DataValue {
        throw std::runtime_error(failure);
    });
    const auto check = [&](const std::string& message, const std::string& expected) {
        failure = message;
        const auto response = channel.Serializer().Deserialize(
            channel.Call(R"({"method":"fail","arguments":null})"));
        Require(response.at("error").at("code") == "handler_failed", "Error code changed.");
        Require(response.at("error").at("message") == expected, "Error truncation damaged UTF-8.");
    };
    check("Short Ω", "Short Ω");
    check(std::string(4096, 'a'), std::string(4096, 'a'));
    check(std::string(4097, 'a'), std::string(4096, 'a'));
    for (const std::string character : {"Ж", "€", "🌍"})
    {
        for (std::size_t included = 1; included < character.size(); ++included)
        {
            const auto prefix = std::string(4096 - included, 'a');
            check(prefix + character, prefix);
        }
        const auto exact = std::string(4096 - character.size(), 'a') + character;
        check(exact, exact);
        check(exact + "tail", exact);
    }
}

void CheckIntegers()
{
    using f4forge::DataValue;
    using f4forge::papyrus_registration::ReadInteger;
    constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
    constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
    Require(ReadInteger(DataValue(minimum)) == minimum, "Minimum integer rejected.");
    Require(ReadInteger(DataValue(maximum)) == maximum, "Maximum integer rejected.");
    Require(ReadInteger(DataValue(std::uint64_t{maximum})) == maximum, "Unsigned maximum rejected.");
    Require(ReadInteger(DataValue(std::uint64_t{0})) == 0, "Unsigned zero rejected.");
    const std::vector<DataValue> outOfRange{
        std::int64_t{minimum} - 1, std::int64_t{maximum} + 1,
        std::uint64_t{maximum} + 1, std::numeric_limits<std::uint64_t>::max(),
        std::numeric_limits<std::int64_t>::min(), std::numeric_limits<std::int64_t>::max()};
    for (const auto& value : outOfRange)
    {
        bool rejected = false;
        try
        {
            ReadInteger(DataValue::parse(value.dump()));
        }
        catch (const std::out_of_range&)
        {
            rejected = true;
        }
        Require(rejected, "Out-of-range Papyrus integer was accepted.");
    }
    for (const auto& value : std::vector<DataValue>{1.5, true, "42", nullptr})
    {
        bool rejected = false;
        try
        {
            ReadInteger(value);
        }
        catch (const std::invalid_argument&)
        {
            rejected = true;
        }
        Require(rejected, "Non-integer Papyrus value was accepted.");
    }
}

struct Renderer
{
    bool valid = true;
    bool focused = true;
    bool destroyed = false;
    bool destroyedWhileFocused = false;

    bool IsValid(int handle) const noexcept { return valid && handle == 7; }
    void Unfocus(int) noexcept { focused = false; }
    void Destroy(int) noexcept
    {
        destroyedWhileFocused = focused;
        destroyed = true;
        valid = false;
    }
};

void CheckCleanup()
{
    Renderer renderer;
    std::vector<std::function<void()>> queue;
    const auto enqueue = [&queue](auto task) { queue.emplace_back(std::move(task)); };
    bool delivered = false;
    {
        f4forge::Channel channel;
        f4forge::game_events::Runtime runtime;
        runtime.Start(channel, [](std::string_view) {});
        queue.emplace_back([&] { runtime.Invoke([&] { delivered = true; }); });
        f4forge::prisma_ui::QueueDestruction(&renderer, 7, enqueue);
        Require(!renderer.destroyed, "Cleanup must wait for the game-thread queue.");
        runtime.Stop();
        queue.front()();
        Require(!delivered, "Stopped runtime still delivers notifications.");
        queue.erase(queue.begin());
    }
    // Execute cleanup after both the runtime and channel have been destroyed.
    queue.front()();
    Require(renderer.destroyed && !renderer.focused && !renderer.destroyedWhileFocused,
            "Shutdown cleanup must unfocus before destroying the view.");
    queue.clear();
    renderer.destroyed = false;
    f4forge::prisma_ui::QueueDestruction(&renderer, 7, enqueue);
    queue.back()();
    Require(!renderer.destroyed, "An invalid view was destroyed twice.");
    f4forge::prisma_ui::QueueDestruction(static_cast<Renderer*>(nullptr), 7, enqueue);
    queue.back()();
}
}

int main()
{
    try
    {
        CheckErrorTruncation(f4forge::CreateDataSerializer());
        CheckErrorTruncation(std::make_shared<StrictSerializer>());
        CheckIntegers();
        CheckCleanup();
        std::cout << "UTF-8, Papyrus integer and shutdown cleanup regressions passed\n";
        return 0;
    }
    catch (const std::exception& exception)
    {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
