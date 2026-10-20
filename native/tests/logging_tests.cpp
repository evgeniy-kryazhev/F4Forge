#include "../modules/logging/logging.h"
#include <iostream>

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
        const auto& serializer = channel.Serializer();
        std::vector<f4forge::DataValue> records;
        const auto registration = f4forge::logging::Register(channel,
            [&](std::string_view level, std::string_view source, std::string_view message) {
                records.push_back({{"level", level}, {"source", source}, {"message", message}});
            });
        const auto call = [&](const f4forge::DataValue& arguments) {
            return serializer.Deserialize(channel.Call(serializer.Serialize({
                {"method", "logging.write"}, {"arguments", arguments}})));
        };
        for (const auto* level : {"info", "warning", "error"}) {
            const f4forge::DataValue arguments = {{"level", level}, {"source", "HelloMod"}, {"message", "Hello 🌍 {value}"}};
            Require(call(arguments).at("value") == true, "Logging failed");
            Require(records.back() == arguments, "Logging changed the payload");
        }
        Require(call({{"level", "fatal"}, {"source", "HelloMod"}, {"message", "test"}}).at("success") == false,
            "Invalid level accepted");
        Require(call({{"level", "info"}, {"source", "bad\nsource"}, {"message", "test"}}).at("success") == false,
            "Invalid source accepted");
        Require(call({{"level", "info"}, {"source", "HelloMod"}, {"message", 42}}).at("success") == false,
            "Invalid message type accepted");
        Require(records.size() == 3, "Invalid request reached the logging sink");
        channel.Remove(registration);
        Require(call(nullptr).at("error").at("code") == "method_not_found", "Logging handler survived removal");
        const auto failing = f4forge::logging::Register(channel, [](auto, auto, auto) {
            throw std::runtime_error("Sink failure");
        });
        Require(call({{"level", "info"}, {"source", "HelloMod"}, {"message", "test"}}).at("error").at("code") == "handler_failed",
            "Sink exception escaped the message boundary");
        channel.Remove(failing);
        std::cout << "Logging payload, validation, cleanup and errors passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
