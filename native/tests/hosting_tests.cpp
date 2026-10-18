#include "components.h"
#include <iostream>

int main()
{
    f4forge::Channel channel;
    f4forge::Components components;
    try {
        const auto dataDirectory = std::filesystem::absolute("build/hosting-test/Data");
        const auto directory = dataDirectory / "F4SE/Plugins";
        const auto modDirectory = dataDirectory / "F4ForgeNet/Nested/Fixture";
        std::filesystem::create_directories(directory);
        std::filesystem::create_directories(modDirectory);
        std::filesystem::copy_file("build/bin/F4Forge.Net.dll", directory / "F4Forge.Net.dll", std::filesystem::copy_options::overwrite_existing);
        std::filesystem::copy_file("tests/dotnet/Fixture/bin/Release/net10.0/Fixture.dll", modDirectory / "Fixture.dll", std::filesystem::copy_options::overwrite_existing);
        std::filesystem::copy_file("tests/dotnet/FixtureDependency/bin/Release/net10.0/FixtureDependency.dll", modDirectory / "FixtureDependency.dll", std::filesystem::copy_options::overwrite_existing);
        channel.Add("native.echo", [](const auto& arguments) {
            return arguments;
        });
        components.Start(channel, directory);
        std::vector<std::string> events;
        channel.Events().Subscribe("fixture.output", [&](const auto& data) {
            events.push_back(data.template get<std::string>());
        });
        channel.Events().Publish("fixture.input", "Native → managed → native event");
        if (events != std::vector<std::string>{"Native → managed → native event"}) {
            throw std::runtime_error("Managed event round trip failed.");
        }
        const auto response = f4forge::Json::parse(channel.Call(R"({"method":"fixture.echo","arguments":{"text":"C++ → .NET → C++"}})"));
        if (response.at("value").at("text") != "C++ → .NET → C++") {
            throw std::runtime_error("Managed mod did not return the expected message.");
        }
        components.Stop();
        channel.Events().Publish("fixture.input", "After stop");
        if (events.size() != 1) {
            throw std::runtime_error("Managed event subscription survived SDK shutdown.");
        }
        const auto removed = f4forge::Json::parse(channel.Call(R"({"method":"fixture.echo","arguments":null})"));
        if (removed.at("error").at("code") != "method_not_found") {
            throw std::runtime_error("Managed handler survived SDK shutdown.");
        }
        std::cout << "Native hosting and managed mod round trip passed\n";
        return 0;
    } catch (const std::exception& exception) {
        components.Stop();
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
