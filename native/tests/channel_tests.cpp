#include "channel.h"
#include <iostream>
#include <stdexcept>

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

int main()
{
    try {
        f4forge::Channel channel;
        const auto registration = channel.Add("test.echo", [](const auto& arguments) {
            return arguments;
        });
        const auto response = f4forge::Json::parse(channel.Call(R"({"method":"test.echo","arguments":{"text":"Привет"}})"));
        Require(response["value"]["text"] == "Привет", "UTF-8 round trip failed");
        Require(f4forge::Json::parse(channel.Call("{"))["error"]["code"] == "invalid_request", "Malformed request accepted");
        Require(f4forge::Json::parse(channel.Call("[]"))["error"]["code"] == "invalid_request", "Array request accepted");
        Require(f4forge::Json::parse(channel.Call(R"({"method":"missing","arguments":null})"))["error"]["code"] == "method_not_found", "Missing method accepted");
        bool duplicateRejected = false;
        try {
            channel.Add("test.echo", [](const auto&) { return nullptr; });
        } catch (const std::invalid_argument&) {
            duplicateRejected = true;
        }
        Require(duplicateRejected, "Duplicate method accepted");
        channel.Add("test.fail", [](const auto&) -> f4forge::Json {
            throw std::runtime_error("Expected failure");
        });
        Require(f4forge::Json::parse(channel.Call(R"({"method":"test.fail","arguments":null})"))["error"]["code"] == "handler_failed", "Exception escaped");
        channel.Add("test.nested", [&channel](const auto& arguments) {
            return f4forge::Json::parse(channel.Call(f4forge::Json{{"method", "test.echo"}, {"arguments", arguments}}.dump()))["value"];
        });
        Require(f4forge::Json::parse(channel.Call(R"({"method":"test.nested","arguments":42})"))["value"] == 42, "Nested call failed");
        std::uint64_t selfRegistration = 0;
        selfRegistration = channel.Add("test.remove_self", [&](const auto&) {
            channel.Remove(selfRegistration);
            return true;
        });
        Require(f4forge::Json::parse(channel.Call(R"({"method":"test.remove_self","arguments":null})"))["value"] == true, "Self removal failed");
        channel.Remove(registration);
        channel.Remove(registration);
        Require(f4forge::Json::parse(channel.Call(R"({"method":"test.echo","arguments":null})"))["error"]["code"] == "method_not_found", "Removed method remains callable");
        Require(f4forge::Json::parse(channel.Call(std::string(f4forge::Channel::MaxMessageBytes + 1, 'x')))["error"]["code"] == "invalid_request", "Oversized request accepted");
        const auto binding = channel.Binding();
        Require(binding.call(binding.context, nullptr, 0, nullptr, nullptr) != 0, "Invalid transport arguments accepted");
        static_assert(sizeof(f4forge::ChannelBinding) == 64);
        std::cout << "Native channel checks passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
