#include "dotnet_sdk.h"
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>

int main()
{
    try {
        const auto data = std::filesystem::absolute(L"build/hosting-test/Loader-Ω/Data");
        const auto plugins = data / L"F4SE/Plugins";
        const auto sdk = plugins / L"F4Forge";
        const auto fixture = data / L"F4ForgeNet/Nested/Fixture";
        std::filesystem::create_directories(sdk);
        std::filesystem::create_directories(fixture);

        constexpr auto overwrite = std::filesystem::copy_options::overwrite_existing;
        for (const auto* name : {L"F4Forge.Net.dll", L"F4Forge.Net.runtimeconfig.json"}) {
            std::filesystem::copy_file(std::filesystem::path(L"build/bin") / name, sdk / name, overwrite);
        }
        const std::filesystem::path binaries = L"tests/dotnet/Fixture/bin/Release/net10.0";
        for (const auto* name : {L"Fixture.dll", L"FixtureDependency.dll"}) {
            std::filesystem::copy_file(binaries / name, fixture / name, overwrite);
        }

        const auto marker = fixture / L"initialized.txt";
        std::filesystem::remove(marker);
        f4forge::LoadManagedPlugins(plugins);
        std::ifstream initialized(marker);
        std::string line;
        if (!std::getline(initialized, line)) {
            throw std::runtime_error("The fixture did not initialize.");
        }
        const std::u8string reportedDirectory(line.begin(), line.end());
        if (std::filesystem::path(reportedDirectory) != fixture) {
            throw std::runtime_error("The fixture did not initialize with its plugin directory.");
        }
        if (std::getline(initialized, line)) {
            throw std::runtime_error("The fixture initialized more than once.");
        }

        std::cout << "Managed plugin initialization passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
