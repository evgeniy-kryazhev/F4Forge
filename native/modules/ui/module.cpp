#include "service.h"
#include "components.h"
#include <RE/Fallout.h>

namespace f4forge::ui {
namespace {

std::unique_ptr<Service> activeService;

const ComponentRegistration component({"ui",
    [](Channel& channel, const std::filesystem::path&) {
        Backend backend;
        backend.isGameThread = [] {
            const auto* main = RE::Main::GetSingleton();
            return main != nullptr && main->threadID != 0
                && main->threadID == REX::W32::GetCurrentThreadId();
        };
        backend.pauseMenuOpen = []() -> std::optional<bool> {
            const auto* ui = RE::UI::GetSingleton();
            if (ui == nullptr) {
                return std::nullopt;
            }
            return ui->GetMenuOpen<RE::PauseMenu>();
        };
        activeService = std::make_unique<Service>(channel, std::move(backend));
    },
    [] {
        activeService.reset();
    }});

}
}
