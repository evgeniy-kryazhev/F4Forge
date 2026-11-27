#include "embedded_sdk.h"

#include <Windows.h>
#include <objbase.h>
#include <fstream>
#include <stdexcept>

// This linker symbol identifies the DLL containing the resources, even inside Fallout4.exe.
extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace f4forge {

std::span<const char> EmbeddedSdkResource(const wchar_t* name)
{
    const auto module = reinterpret_cast<HMODULE>(&__ImageBase);
    const auto resource = FindResourceW(module, name, MAKEINTRESOURCEW(10)); // RT_RCDATA
    if (resource == nullptr) {
        throw std::runtime_error("Missing embedded .NET SDK resource.");
    }
    const auto size = SizeofResource(module, resource);
    const auto loaded = LoadResource(module, resource);
    const auto bytes = loaded == nullptr ? nullptr : static_cast<const char*>(LockResource(loaded));
    if (bytes == nullptr || size == 0) {
        throw std::runtime_error("Cannot read embedded .NET SDK resource.");
    }
    return {bytes, size};
}

TemporaryRuntimeConfig::TemporaryRuntimeConfig()
{
    GUID id{};
    wchar_t name[40]{};
    if (FAILED(CoCreateGuid(&id)) || StringFromGUID2(id, name, 40) == 0) {
        throw std::runtime_error("Cannot name the temporary .NET configuration directory.");
    }
    directory_ = std::filesystem::temp_directory_path() / name;
    path_ = directory_ / L"F4Forge.Net.runtimeconfig.json";
    if (!std::filesystem::create_directory(directory_)) {
        throw std::runtime_error("Cannot create the temporary .NET configuration directory.");
    }
}

void TemporaryRuntimeConfig::Write(std::span<const char> contents) const
{
    std::ofstream output(path_, std::ios::binary);
    output.exceptions(std::ios::failbit | std::ios::badbit);
    output.write(contents.data(), static_cast<std::streamsize>(contents.size()));
    output.close();
}

TemporaryRuntimeConfig::~TemporaryRuntimeConfig()
{
    std::error_code error;
    std::filesystem::remove(path_, error);
    std::filesystem::remove(directory_, error);
}

}
