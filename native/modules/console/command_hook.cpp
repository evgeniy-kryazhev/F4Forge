#include "command_hook.h"
#include <MinHook.h>
#include <stdexcept>
#include <string>

namespace f4forge::console {
namespace {

void Check(MH_STATUS status, const char* operation)
{
    if (status != MH_OK) {
        throw std::runtime_error(std::string(operation) + ": " + MH_StatusToString(status));
    }
}

}

void CommandHook::Install(Function target, Function replacement)
{
    std::lock_guard lock(gate_);
    if (target == nullptr || replacement == nullptr) {
        throw std::invalid_argument("Console hook target and replacement are required.");
    }
    if (target_ != nullptr) {
        if (target_ != target || replacement_ != replacement) {
            throw std::logic_error("Console hook is already installed on another function.");
        }
        return;
    }
    const auto initialized = MH_Initialize();
    if (initialized != MH_ERROR_ALREADY_INITIALIZED) {
        Check(initialized, "Initialize console hook");
    }
    void* original = nullptr;
    auto* address = reinterpret_cast<void*>(target);
    Check(MH_CreateHook(address, reinterpret_cast<void*>(replacement), &original), "Create console hook");
    original_.store(reinterpret_cast<Function>(original), std::memory_order_release);
    const auto enabled = MH_EnableHook(address);
    if (enabled != MH_OK) {
        // No callback can have entered a hook that failed to enable.
        Check(MH_RemoveHook(address), "Roll back console hook");
        original_.store(nullptr, std::memory_order_release);
        Check(enabled, "Enable console hook");
    }
    target_ = target;
    replacement_ = replacement;
}

}
