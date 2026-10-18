#include "components.h"
#include <iostream>
#include <stdexcept>

namespace {

std::vector<std::string> events;
bool failSecondSdk = false;

const f4forge::ComponentRegistration nativeModule({
    "z.native", [](auto& channel, const auto&) {
        events.push_back("native.start");
        channel.Add("native.ready", [](const auto&) { return true; });
    }, [] { events.push_back("native.stop"); }
});

const f4forge::ComponentRegistration firstSdk({
    "a.sdk", [](auto& channel, const auto&) {
        const auto result = f4forge::Json::parse(channel.Call(R"({"method":"native.ready","arguments":null})"));
        if (!result.at("value").template get<bool>()) {
            throw std::runtime_error("Native module was not ready before SDK startup.");
        }
        events.push_back("first.start");
    }, [] { events.push_back("first.stop"); }, f4forge::ComponentPhase::Sdk
});

const f4forge::ComponentRegistration secondSdk({
    "b.sdk", [](auto&, const auto&) {
        events.push_back("second.start");
        if (failSecondSdk) {
            throw std::runtime_error("Expected SDK startup failure.");
        }
    }, [] { events.push_back("second.stop"); }, f4forge::ComponentPhase::Sdk
});

}

int main()
{
    try {
        for (const bool failure : {false, true}) {
            events.clear();
            failSecondSdk = failure;
            f4forge::Channel channel;
            f4forge::Components components;
            bool startFailed = false;
            try {
                components.Start(channel, {});
            } catch (const std::runtime_error&) {
                startFailed = true;
            }
            if (startFailed != failure) {
                throw std::runtime_error("Unexpected startup outcome.");
            }
            components.Stop();
            components.Stop();
            const std::vector<std::string> expected{
                "native.start", "first.start", "second.start", "second.stop", "first.stop", "native.stop"
            };
            if (events != expected) {
                throw std::runtime_error("Component ordering or rollback failed.");
            }
        }
        std::cout << "Multiple SDKs, startup phases and rollback passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
