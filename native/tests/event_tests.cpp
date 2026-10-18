#include "channel.h"
#include <iostream>
#include <stdexcept>

namespace {

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

}

int main()
{
    try {
        f4forge::Channel channel;
        auto& events = channel.Events();
        std::vector<int> received;
        std::vector<std::string> failures;
        events.SetErrorSink([&](std::string_view message) {
            failures.emplace_back(message);
        });
        events.Publish("test.event", 0); // No replay for future subscribers.
        const auto first = events.Subscribe("test.event", [&](const auto& value) {
            received.push_back(value.template get<int>());
        });
        events.Subscribe("test.event", [](const auto&) {
            throw std::runtime_error("Subscriber failed");
        });
        events.Subscribe("test.event", [&](const auto&) {
            received.push_back(2);
        });
        events.Publish("test.event", 1);
        Require(received == std::vector<int>{1, 2}, "Order or exception isolation failed");
        Require(failures.size() == 1, "Subscriber failure was not logged");
        events.Unsubscribe(first);
        events.Unsubscribe(first);
        received.clear();
        events.Publish("test.event", 3);
        Require(received == std::vector<int>{2}, "Removed subscriber was called");

        std::uint64_t removedBeforeTurn = 0;
        events.Subscribe("mutation", [&](const auto&) {
            events.Unsubscribe(removedBeforeTurn);
            events.Subscribe("mutation", [&](const auto&) {
                received.push_back(9);
            });
        });
        removedBeforeTurn = events.Subscribe("mutation", [&](const auto&) {
            throw std::runtime_error("Removed subscriber executed");
        });
        received.clear();
        events.Publish("mutation", nullptr);
        Require(received.empty(), "New subscription received an old publication");
        events.Publish("mutation", nullptr);
        Require(received == std::vector<int>{9}, "New subscription missed the next publication");

        std::uint64_t self = 0;
        self = events.Subscribe("self", [&](const auto&) {
            events.Unsubscribe(self);
            events.Publish("self", nullptr);
            received.push_back(10);
        });
        events.Publish("self", nullptr);
        Require(received == std::vector<int>({9, 10}), "Self-unsubscribe or nested publication failed");

        bool cleanedUp = false;
        const auto cleanupTarget = events.Subscribe("cleanup.target", [](const auto&) {});
        auto cleanup = std::shared_ptr<bool>(&cleanedUp, [&](bool* flag) {
            events.Unsubscribe(cleanupTarget);
            *flag = true;
        });
        const auto cleanupOwner = events.Subscribe("cleanup.owner", [cleanup](const auto&) {});
        cleanup.reset();
        events.Unsubscribe(cleanupOwner);
        Require(cleanedUp, "Subscription capture cleanup did not complete");

        channel.Add("nested", [&](const auto& data) {
            events.Publish("nested", data);
            return data;
        });
        events.Subscribe("nested", [&](const auto& data) {
            received.push_back(data.template get<int>());
        });
        const auto& serializer = channel.Serializer();
        const auto request = serializer.Serialize({{"method", "nested"}, {"arguments", 11}});
        Require(serializer.Deserialize(channel.Call(request))["value"] == 11, "Message to event call failed");
        Require(received.back() == 11, "Nested event was not delivered");
        Require(serializer.Deserialize(channel.Publish("{"))["error"]["code"] == "invalid_request", "Malformed event accepted");
        Require(serializer.Deserialize(channel.Publish(serializer.Serialize({{"event", 1}, {"data", nullptr}})))["error"]["code"] == "invalid_request", "Invalid event name accepted");
        Require(serializer.Deserialize(channel.Publish(std::string(f4forge::Channel::MaxMessageBytes + 1, 'x')))["error"]["code"] == "invalid_request", "Oversized event accepted");
        std::cout << "Event ordering, mutation, reentrancy, errors and validation passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
