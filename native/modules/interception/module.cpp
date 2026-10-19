#include "registry.h"
#include "components.h"
#include <REX/REX.h>

namespace f4forge::interception {
namespace {

std::unique_ptr<Registry>& Instance()
{
    static std::unique_ptr<Registry> registry;
    return registry;
}

const ComponentRegistration component({"interceptors",
    [](Channel& channel, const std::filesystem::path&) {
        Instance() = std::make_unique<Registry>(channel, [](std::string_view error) {
            REX::ERROR("Interceptor: {}", error);
        });
    },
    [] { Instance().reset(); }});

}

Registry* Active() noexcept
{
    return Instance().get();
}

}
