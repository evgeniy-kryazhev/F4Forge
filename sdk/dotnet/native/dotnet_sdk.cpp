#include "components.h"

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>
#define NETHOST_USE_AS_STATIC
#include <nethost.h>
#include <hostfxr.h>
#include <coreclr_delegates.h>
#include <fstream>
#include <memory>
#include <stdexcept>
#include <vector>

namespace {

struct BootstrapArguments {
    f4forge::ChannelBinding channel;
    const wchar_t* directory;
};

using StartManaged = int (*)(const BootstrapArguments*) noexcept;
using StopManaged = int (*)() noexcept;
StopManaged stopManaged = nullptr;

struct LibraryDeleter {
    void operator()(HMODULE module) const noexcept
    {
        FreeLibrary(module);
    }
};
using Library = std::unique_ptr<std::remove_pointer_t<HMODULE>, LibraryDeleter>;
Library hostLibrary;

class RuntimeConfiguration final {
public:
    RuntimeConfiguration()
    {
        wchar_t directory[MAX_PATH + 1]{};
        wchar_t filename[MAX_PATH + 1]{};
        const auto length = GetTempPathW(MAX_PATH + 1, directory);
        if (length == 0 || length > MAX_PATH || GetTempFileNameW(directory, L"F4F", 0, filename) == 0) {
            throw std::runtime_error("Cannot create the temporary .NET runtime configuration.");
        }
        path_ = filename;
    }

    void Write()
    {
        std::ofstream output(path_, std::ios::binary | std::ios::trunc);
        output << R"({"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}})";
        output.close();
        if (!output) {
            throw std::runtime_error("Cannot write the .NET runtime configuration.");
        }
        // hostfxr replaces the supplied extension with .json before reading it.
        auto configurationPath = path_;
        configurationPath.replace_extension(L".json");
        std::filesystem::rename(path_, configurationPath);
        path_ = std::move(configurationPath);
    }

    ~RuntimeConfiguration()
    {
        std::error_code error;
        std::filesystem::remove(path_, error);
    }

    const wchar_t* Path() const noexcept
    {
        return path_.c_str();
    }

private:
    std::filesystem::path path_;
};

template<class Function>
Function Export(HMODULE library, const char* name)
{
    const auto address = GetProcAddress(library, name);
    if (address == nullptr) {
        throw std::runtime_error(std::string("Missing hostfxr export: ") + name);
    }
    return reinterpret_cast<Function>(address);
}

void Start(f4forge::Channel& channel, const std::filesystem::path& directory)
{
    // F4SE scans DLLs directly in Plugins as native plugins. Keep managed SDKs
    // below that directory so they cannot interrupt its PE export scan.
    const auto assembly = std::filesystem::absolute(directory / L"F4Forge/F4Forge.Net.dll");
    if (!std::filesystem::exists(assembly)) {
        return; // SDKs are optional; only deployed SDK assemblies are started.
    }

    std::vector<wchar_t> hostPath(32768);
    auto hostPathSize = hostPath.size();
    if (get_hostfxr_path(hostPath.data(), &hostPathSize, nullptr) != 0) {
        throw std::runtime_error("Install the .NET 10 x64 runtime to load F4Forge.Net.");
    }
    hostLibrary.reset(LoadLibraryW(hostPath.data()));
    if (!hostLibrary) {
        throw std::runtime_error("Cannot load hostfxr.");
    }
    const auto initialize = Export<hostfxr_initialize_for_runtime_config_fn>(hostLibrary.get(), "hostfxr_initialize_for_runtime_config");
    const auto getDelegate = Export<hostfxr_get_runtime_delegate_fn>(hostLibrary.get(), "hostfxr_get_runtime_delegate");
    const auto close = Export<hostfxr_close_fn>(hostLibrary.get(), "hostfxr_close");
    RuntimeConfiguration configuration;
    configuration.Write();
    hostfxr_handle context = nullptr;
    const auto initializeResult = initialize(configuration.Path(), nullptr, &context);
    if (initializeResult < 0 || context == nullptr) {
        if (context != nullptr) {
            close(context);
        }
        throw std::runtime_error("Cannot initialize the .NET 10 runtime.");
    }
    void* loaderAddress = nullptr;
    const auto delegateResult = getDelegate(context, hdt_load_assembly_and_get_function_pointer, &loaderAddress);
    close(context);
    if (delegateResult < 0 || loaderAddress == nullptr) {
        throw std::runtime_error("Cannot acquire the .NET assembly loader.");
    }
    const auto load = reinterpret_cast<load_assembly_and_get_function_pointer_fn>(loaderAddress);
    const auto typeName = L"F4Forge.Net.Hosting.Bootstrap, F4Forge.Net";
    void* startAddress = nullptr;
    void* stopAddress = nullptr;
    if (load(assembly.c_str(), typeName, L"Start", UNMANAGEDCALLERSONLY_METHOD, nullptr, &startAddress) != 0
        || load(assembly.c_str(), typeName, L"Stop", UNMANAGEDCALLERSONLY_METHOD, nullptr, &stopAddress) != 0) {
        throw std::runtime_error("Cannot find F4Forge.Net bootstrap methods.");
    }
    stopManaged = reinterpret_cast<StopManaged>(stopAddress);
    const BootstrapArguments arguments{channel.Binding(), directory.c_str()};
    const auto start = reinterpret_cast<StartManaged>(startAddress);
    if (start(&arguments) != 0) {
        throw std::runtime_error("F4Forge.Net startup failed; see F4Forge/Net.log.");
    }
}

void Stop()
{
    if (stopManaged != nullptr) {
        const auto stop = std::exchange(stopManaged, nullptr);
        if (stop() != 0) {
            throw std::runtime_error("F4Forge.Net shutdown failed.");
        }
    }
    hostLibrary.reset();
}

const f4forge::ComponentRegistration registration({"sdk.dotnet", &Start, &Stop, f4forge::ComponentPhase::Sdk});

}
