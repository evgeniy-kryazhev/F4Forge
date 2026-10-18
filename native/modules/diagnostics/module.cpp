#include "store.h"
#include "components.h"
#include <REX/REX.h>

namespace {

std::unique_ptr<f4forge::diagnostics::Service>& Service()
{
    static std::unique_ptr<f4forge::diagnostics::Service> service;
    return service;
}

const f4forge::ComponentRegistration component({"diagnostics",
    [](f4forge::Channel& channel, const std::filesystem::path&) {
        f4forge::diagnostics::Global().SetErrorSink([](std::string_view text) { REX::ERROR("Diagnostics: {}", text); });
        Service() = std::make_unique<f4forge::diagnostics::Service>(channel, f4forge::diagnostics::Global());
    },
    [] {
        Service().reset();
        f4forge::diagnostics::Global().SetErrorSink({});
    }});

}
