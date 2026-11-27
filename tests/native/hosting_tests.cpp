#include "channel.h"
#include <F4SE/F4SE.h>
#include <RE/N/NiPoint3.h>
#include <Windows.h>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <memory>
#include <stdexcept>
#include <string>
#include <string_view>

int main(int argc, char* argv[])
{
    try {
        const bool packagedHost = argc == 2 && std::string_view(argv[1]) == "--package";
        if (argc > 1 && !packagedHost) {
            throw std::invalid_argument("Usage: F4Forge.Hosting.Tests [--package]");
        }
        const auto installation = packagedHost ? L"SingleFile-Ω" : L"Invoke-Ω";
        const auto data = std::filesystem::absolute(std::filesystem::path(L"build/hosting-test") / installation / L"Data");
        const auto plugins = data / L"F4SE/Plugins";
        const auto fixture = data / L"F4ForgeNet/Nested/Fixture";
        std::filesystem::create_directories(plugins);
        std::filesystem::create_directories(fixture);

        constexpr auto overwrite = std::filesystem::copy_options::overwrite_existing;
        const auto plugin = plugins / L"F4Forge.dll";
        const auto source = packagedHost ? L"build/dist/F4Forge.dll" : L"build/tests/host/F4Forge.dll";
        std::filesystem::copy_file(source, plugin, overwrite);
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
        const auto probeDirectory = data / L"F4ForgeNet/BridgeProbe";
        const auto bridgeResult = probeDirectory / L"bridge-result.txt";
        if (!packagedHost) {
            std::filesystem::create_directories(probeDirectory);
            std::filesystem::copy_file(L"tests/dotnet/BridgeProbe/bin/Release/net10.0/BridgeProbe.dll",
                probeDirectory / L"BridgeProbe.dll", overwrite);
            std::filesystem::remove(bridgeResult);
        }
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
        static F4SE::MessagingInterface::EventCallback* onMessage = nullptr;
        static F4SE::Impl::F4SEMessagingInterface messaging{};
        messaging.interfaceVersion = F4SE::MessagingInterface::kVersion;
        messaging.RegisterListener = [](std::uint32_t, const char* sender, void* callback) {
            if (std::string_view(sender) != "F4SE" || !callback) {
                return false;
            }
            onMessage = reinterpret_cast<F4SE::MessagingInterface::EventCallback*>(callback);
            return true;
        };
        F4SE::Impl::F4SEInterface host{};
        host.QueryInterface = [](std::uint32_t id) -> void* {
            return id == F4SE::LoadInterface::kMessaging ? &messaging : nullptr;
        };
        host.GetPluginHandle = []() -> std::uint32_t { return 1; };
        host.GetReleaseIndex = []() -> std::uint32_t { return 0; };
        host.GetSaveFolderName = []() { return ""; };
        if (!load(reinterpret_cast<const F4SE::LoadInterface*>(&host))) {
            throw std::runtime_error("F4SE plugin initialization failed.");
        }
        if (!onMessage) {
            throw std::runtime_error("The host did not subscribe to F4SE messages.");
        }
        F4SE::MessagingInterface::Message beforeDataReady{"F4SE", F4SE::MessagingInterface::kGameDataReady, 1, nullptr};
        onMessage(&beforeDataReady);
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

        if (packagedHost) {
            const auto invoke = reinterpret_cast<f4forge::InvokeFunction>(GetProcAddress(library.get(), "InvokeFFI"));
            if (invoke == nullptr) {
                throw std::runtime_error("Missing InvokeFFI export.");
            }
            f4forge::F4SEValue result{};
            invoke("Test_Add", nullptr, 0, &result);
            if (result.type != f4forge::ValueType::Error || result.error != f4forge::InvokeError::UnknownOperation) {
                throw std::runtime_error("The packaged DLL must not register Test_Add.");
            }
            constexpr RE::NiPoint3 point{3.0f, 4.0f, 0.0f};
            f4forge::F4SEValue self{f4forge::ValueType::Pointer, {.pointer = &point}};
            invoke("RE::NiPoint3::SqrLength() const", &self, 1, &result);
            if (result.type != f4forge::ValueType::Float || result.number != 25.0f) {
                throw std::runtime_error("Packaged host could not call the generated NiPoint3 binding.");
            }
            self.pointer = nullptr;
            invoke("RE::NiPoint3::SqrLength() const", &self, 1, &result);
            if (result.type != f4forge::ValueType::Error || result.error != f4forge::InvokeError::InvalidArguments) {
                throw std::runtime_error("Generated instance binding accepted a null receiver.");
            }
            std::cout << "Packaged host has no Test_Add binding\n";
        } else {
            std::ifstream result(bridgeResult);
            if (!std::getline(result, line) || line != "5") {
                throw std::runtime_error("The C# bridge probe did not receive 5 from Test_Add(2, 3).");
            }
            std::cout << "Bridge.Invoke<int>(Test_Add, 2, 3) returned " << line << '\n';
            std::cout << "C# plugin passed the generated CommonLibF4 binding checks\n";
            std::cout << "C# plugin received the generated TESObjectLoadedEvent and unsubscribed\n";
        }
        std::cout << "Managed plugin initialization passed\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
