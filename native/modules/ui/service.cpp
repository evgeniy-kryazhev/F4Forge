#include "service.h"
#include <stdexcept>
#include <utility>

namespace f4forge::ui {

Service::Service(Channel& channel, Backend backend) : channel_(channel), registration_(0)
{
    if (!backend.isGameThread || !backend.pauseMenuOpen) {
        throw std::invalid_argument("A complete UI backend is required.");
    }
    // Own the backend in the callback so removal cannot invalidate callback state.
    registration_ = channel_.Add("ui.is_pause_menu_open", [backend = std::move(backend)](const DataValue&) {
        if (!backend.isGameThread()) {
            throw std::runtime_error("UI queries must execute on the game thread. Use GameThread.RunAsync.");
        }
        return backend.pauseMenuOpen().value_or(false);
    });
}

Service::~Service()
{
    channel_.Remove(registration_);
}

}
