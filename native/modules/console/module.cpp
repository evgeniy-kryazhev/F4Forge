#include "router.h"
#include "command_hook.h"
#include "dispatch.h"
#include "components.h"
#include "../diagnostics/store.h"
#include <RE/C/Console.h>
#include <RE/C/ConsoleLog.h>
#include <RE/S/SCRIPT_FUNCTION.h>
#include <RE/T/TESObjectREFR.h>
#include <REL/Relocation.h>
#include <REX/REX.h>
#include <type_traits>

namespace f4forge::console {
namespace {

static_assert(std::is_same_v<CommandHook::Function, decltype(&RE::Console::ExecuteCommand)>);

CommandHook hook;

std::unique_ptr<Router>& ActiveRouter()
{
    // Construct after the host channel, so process teardown destroys the router
    // first even when F4SE exits without explicitly stopping the components.
    static std::unique_ptr<Router> router;
    return router;
}

bool Matches(const char* name, std::string_view alias)
{
    return name != nullptr && std::strlen(name) == alias.size() && _strnicmp(name, alias.data(), alias.size()) == 0;
}

bool IsReserved(std::string_view alias)
{
    for (const auto& command : RE::SCRIPT_FUNCTION::GetConsoleFunctions()) {
        if (Matches(command.functionName, alias) || Matches(command.shortName, alias)) {
            return true;
        }
    }
    for (const auto& command : RE::SCRIPT_FUNCTION::GetScriptFunctions()) {
        if (Matches(command.functionName, alias) || Matches(command.shortName, alias)) {
            return true;
        }
    }
    return false;
}

void WriteLine(std::string_view line)
{
    if (auto* console = RE::ConsoleLog::GetSingleton()) {
        console->Log("{}", line);
    }
}

void OnCommand(const char* text) noexcept
{
    ForwardCommand(game_events::Host(), text, [](const char* input) {
        const auto selected = RE::Console::GetCurrentPickREFR().get();
        const auto& router = ActiveRouter();
        return router != nullptr && router->Execute(input, selected ? selected->GetFormID() : 0);
    }, [](const char* input) {
        hook.Original()(input);
    });
}

const game_events::AdapterRegistration adapter({
    "console.commands",
    [](game_events::Runtime& runtime, const game_events::Message&) {
        if (runtime.DataReady()) {
            // CommonLib supplies the function signature; Address Library resolves
            // the entry point. MinHook relocates its instructions, not a guessed offset.
            const REL::Relocation<CommandHook::Function> target{RE::ID::Console::ExecuteCommand};
            diagnostics::CheckAndInstall("console.execute", "function", [&] {
                diagnostics::CheckCode(target.address());
                hook.Install(target.get(), &OnCommand);
            });
        } else {
            diagnostics::Global().Hook("console.execute", "function", "inactive", "Game data is unavailable.");
        }
    },
    [] {
        // Runtime::Stop has disabled and drained routing before this callback.
        // Retain the entry hook as an inactive pass-through, including its original
        // trampoline, so an already-entered engine call can safely finish.
        diagnostics::Global().Hook("console.execute", "function", "inactive", "Host stopped; original trampoline retained.");
    }
});

const ComponentRegistration component({
    "console",
    [](Channel& channel, const std::filesystem::path&) {
        ActiveRouter() = std::make_unique<Router>(channel, WriteLine, IsReserved);
        REX::INFO("Console command module initialized");
    },
    [] {
        // Components stop in reverse order: game_events drains leases first,
        // then this module can release the router and its validation registration.
        ActiveRouter().reset();
    }
});

}
}
