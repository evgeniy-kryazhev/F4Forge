#include "bindings.h"
#include "components.h"
#include <RE/M/Main.h>
#include "generated.inc"

namespace f4forge::singletons {
namespace {

std::unique_ptr<Bindings> activeBindings;

const ComponentRegistration component({"singletons",
    [](Channel& channel, const std::filesystem::path&) {
        auto bindings = std::make_unique<Bindings>(channel, [] {
            const auto* main = RE::Main::GetSingleton();
            return main != nullptr && main->threadID != 0
                && main->threadID == REX::W32::GetCurrentThreadId();
        });
        // A failed registration destroys the local owner and rolls back all prior handlers.
        RegisterGenerated(*bindings);
        activeBindings = std::move(bindings);
    },
    [] {
        activeBindings.reset();
    }});

}
}
