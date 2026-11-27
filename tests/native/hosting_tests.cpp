#include <F4SE/F4SE.h>
#include <Windows.h>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>

int main()
{
    try {
        const auto data = std::filesystem::absolute(L"build/hosting-test/SingleFile-Ω/Data");
        const auto plugins = data / L"F4SE/Plugins";
        const auto fixture = data / L"F4ForgeNet/Nested/Fixture";
        std::filesystem::create_directories(plugins);
        std::filesystem::create_directories(fixture);

        constexpr auto overwrite = std::filesystem::copy_options::overwrite_existing;
        const auto plugin = plugins / L"F4Forge.dll";
        std::filesystem::copy_file(L"build/dist/F4Forge.dll", plugin, overwrite);
        for (const auto& entry : std::filesystem::directory_iterator(plugins)) {
            if (entry.path() != plugin) {
                throw std::runtime_error("Expected only F4Forge.dll in the test installation.");
            }
        }
        const std::filesystem::path binaries = L"tests/dotnet/Fixture/bin/Release/net10.0";
        for (const auto* name : {L"Fixture.dll", L"FixtureDependency.dll"}) {
            std::filesystem::copy_file(binaries / name, fixture / name, overwrite);
        }

        const auto marker = fixture / L"initialized.txt";
        std::filesystem::remove(marker);
        const std::unique_ptr<std::remove_pointer_t<HMODULE>, decltype(&FreeLibrary)> library(
            LoadLibraryW(plugin.c_str()), &FreeLibrary);
        if (!library) {
            throw std::runtime_error("Cannot load the packaged F4Forge.dll.");
        }
        using LoadPlugin = bool (*)(const F4SE::LoadInterface*);
        const auto load = reinterpret_cast<LoadPlugin>(GetProcAddress(library.get(), "F4SEPlugin_Load"));
        if (load == nullptr) {
            throw std::runtime_error("Missing F4SEPlugin_Load export.");
        }
        // Exercise the production entry point with no game services or game log directory.
        F4SE::Impl::F4SEInterface host{};
        host.QueryInterface = [](std::uint32_t) -> void* { return nullptr; };
        host.GetPluginHandle = []() -> std::uint32_t { return 1; };
        host.GetReleaseIndex = []() -> std::uint32_t { return 0; };
        host.GetSaveFolderName = []() { return ""; };
        if (!load(reinterpret_cast<const F4SE::LoadInterface*>(&host))) {
            throw std::runtime_error("F4SE plugin initialization failed.");
        }
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
