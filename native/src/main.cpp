#include "../core/config/config.h"
#include "../core/f4forge_host.h"
#include "../core/modules/builtin_services.h"
#include "f4se_game_scheduler.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#ifdef ERROR
#undef ERROR
#endif
#include <filesystem>
#include <string>
#include <string_view>
#include <vector>
#include <memory>

namespace {

f4forge::native::F4seGameScheduler gameScheduler;
std::unique_ptr<f4forge::core::BuiltinServices> builtinServices;

void F4SEAPI OnMessage(F4SE::MessagingInterface::Message* message)
{
    try {
        if (message == nullptr || builtinServices == nullptr) return;
        switch (message->type) {
        case F4SE::MessagingInterface::kGameDataReady:
            if (message->data == nullptr) return;
            builtinServices->OnGameDataReady();
            break;
        case F4SE::MessagingInterface::kPostLoadGame:
            builtinServices->OnGameLoaded();
            break;
        case F4SE::MessagingInterface::kNewGame:
            builtinServices->OnNewGame();
            break;
        default:
            break;
        }
    } catch (...) {
        REX::ERROR("F4Forge: framework message dispatch failed");
    }
}

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

std::filesystem::path FrameworkDirectory()
{
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&FrameworkDirectory),
            &module))
    {
        REX::ERROR("F4Forge: GetModuleHandleExW failed ({})", GetLastError());
        return {};
    }

    std::vector<wchar_t> buffer(32768);
    const auto length = GetModuleFileNameW(module, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size()) {
        REX::ERROR("F4Forge: GetModuleFileNameW failed ({})", GetLastError());
        return {};
    }
    return std::filesystem::path(buffer.data(), buffer.data() + length).parent_path() / L"F4Forge";
}

}

extern "C" __declspec(dllexport) const F4ForgeHostApi* F4FORGE_CALL
F4ForgeGetHostApi(void) F4FORGE_NOEXCEPT
{
    return &f4forge::core::F4ForgeHost::Instance().Api();
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
		REX::INFO("F4Forge: load entered");
		const auto frameworkDirectory = FrameworkDirectory();
		if (frameworkDirectory.empty()) {
			REX::ERROR("F4Forge: framework directory is empty");
			return true;
		}
		REX::INFO("F4Forge: framework directory = {}", frameworkDirectory.string());
		const auto config = f4forge::core::ConfigLoader::Load(
			frameworkDirectory / L"F4Forge.toml", frameworkDirectory);
		REX::INFO("F4Forge: plugin directory = {}", config.pluginDirectory.string());
		REX::INFO("F4Forge: runtime directory = {}", config.runtimeDirectory.string());
		auto& host = f4forge::core::F4ForgeHost::Instance();
		gameScheduler.CaptureGameThread();
		host.SetGameThreadScheduler(&gameScheduler);
		host.Endpoints().SetGameThreadCheck(&f4forge::native::F4seGameScheduler::CheckGameThread);
		host.SetLogSink(&NativeLog);
        builtinServices = std::make_unique<f4forge::core::BuiltinServices>(host.Endpoints(), host.Events());
        builtinServices->Start();
        REX::INFO("F4Forge: input endpoint = {}, menu handler = {}, gameplay handler = {}",
            builtinServices->Input().KeyDownEndpoint(), builtinServices->Input().IsMenuHandlerInstalled(),
            builtinServices->Input().IsGameplayHandlerInstalled());
		const auto* messaging = F4SE::GetMessagingInterface();
		if (messaging == nullptr || !messaging->RegisterListener(&OnMessage)) {
			REX::ERROR("F4Forge: failed to register F4SE messaging listener");
			return false;
		}
		auto& runtimes = host.Runtimes();
		const auto discovered = runtimes.DiscoverDirectory(config.runtimeDirectory);
		REX::INFO("F4Forge: runtime providers discovered = {}", discovered);
		const auto pluginDirectory = config.pluginDirectory.u8string();
		const auto configDirectory = frameworkDirectory.u8string();
		const auto initialized = runtimes.InitializeAll(
			&host.Api(),
			{ reinterpret_cast<const char*>(pluginDirectory.data()), static_cast<uint32_t>(pluginDirectory.size()) },
			{ reinterpret_cast<const char*>(configDirectory.data()), static_cast<uint32_t>(configDirectory.size()) });
		REX::INFO("F4Forge: runtimes initialized = {}", initialized);
		return true;
	} catch (const std::exception& exception) {
		REX::ERROR("F4Forge: loader exception: {}", exception.what());
		return false;
	} catch (...) {
		REX::ERROR("F4Forge: loader exception: unknown");
		return false;
	}
}
