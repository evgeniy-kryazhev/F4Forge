#include "components.h"
#include <iostream>

int main()
{
    f4forge::Channel channel;
    f4forge::Components components;
    try {
        const auto directory = std::filesystem::absolute("build/hosting-test");
        const auto modDirectory = directory / "F4Forge/mods/dotnet/Fixture";
        std::filesystem::create_directories(modDirectory);
        std::filesystem::copy_file("build/bin/F4Forge.Net.dll", directory / "F4Forge.Net.dll", std::filesystem::copy_options::overwrite_existing);
        std::filesystem::copy_file("tests/dotnet/Fixture/bin/Release/net10.0/Fixture.dll", modDirectory / "Fixture.dll", std::filesystem::copy_options::overwrite_existing);
        std::filesystem::copy_file("tests/dotnet/Fixture/mod.json", modDirectory / "mod.json", std::filesystem::copy_options::overwrite_existing);
        channel.Add("native.echo", [](const auto& arguments) {
            return arguments;
        });
        components.Start(channel, directory);
        const auto response = f4forge::Json::parse(channel.Call(R"({"method":"fixture.echo","arguments":{"text":"C++ → .NET → C++"}})"));
        if (response.at("value").at("text") != "C++ → .NET → C++") {
            throw std::runtime_error("Managed mod did not return the expected message.");
        }
        components.Stop();
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
