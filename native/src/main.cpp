#include "../core/config/config.h"
#include "../core/f4forge_host.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#ifdef ERROR
#undef ERROR
#endif
#include <array>
#include <filesystem>
#include <string>
#include <string_view>

namespace {

void NativeLog(uint32_t level, std::string_view message) noexcept
{
	try {
		const std::string text(message);
		switch (level) {
		case 0:
			REX::TRACE("{}", text);
			break;
		case 1:
			REX::DEBUG("{}", text);
			break;
		case 3:
			REX::INFO("[warning] {}", text);
			break;
		case 4:
		case 5:
			REX::ERROR("{}", text);
			break;
		default:
			REX::INFO("{}", text);
			break;
		}
	} catch (...) {
	}
}

std::filesystem::path FrameworkDirectory() noexcept
{
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&FrameworkDirectory),
            &module))
        return {};

    std::array<wchar_t, 32768> buffer{};
    const auto length = GetModuleFileNameW(module, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size()) return {};
    return std::filesystem::path(buffer.data(), buffer.data() + length).parent_path() / L"F4Forge";
}

}

F4SE_PLUGIN_PRELOAD(const F4SE::PreLoadInterface* a_f4se)
{
	F4SE::Init(a_f4se);
	return true;
}

F4SE_PLUGIN_LOAD(const F4SE::LoadInterface* a_f4se)
{
	try {
		F4SE::Init(a_f4se);
		const auto frameworkDirectory = FrameworkDirectory();
		if (frameworkDirectory.empty()) return true;
		const auto config = f4forge::core::ConfigLoader::Load(
			frameworkDirectory / L"F4Forge.toml", frameworkDirectory);
		auto& host = f4forge::core::F4ForgeHost::Instance();
		host.SetLogSink(&NativeLog);
		auto& runtimes = host.Runtimes();
		runtimes.DiscoverDirectory(config.runtimeDirectory);
		const auto pluginDirectory = config.pluginDirectory.u8string();
		const auto configDirectory = frameworkDirectory.u8string();
		runtimes.InitializeAll(
			&host.Api(),
			{ reinterpret_cast<const char*>(pluginDirectory.data()), static_cast<uint32_t>(pluginDirectory.size()) },
			{ reinterpret_cast<const char*>(configDirectory.data()), static_cast<uint32_t>(configDirectory.size()) });
		return true;
	} catch (...) {
		return false;
	}
}
