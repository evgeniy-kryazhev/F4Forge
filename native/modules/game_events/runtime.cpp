#include "runtime.h"
#include <algorithm>
#include <set>
#include <stdexcept>
#include <tuple>

namespace f4forge::game_events {
namespace {

std::vector<Adapter>& Registrations()
{
    static std::vector<Adapter> adapters;
    return adapters;
}

}

AdapterRegistration::AdapterRegistration(Adapter adapter)
{
    Registrations().push_back(std::move(adapter));
}

Runtime& Host()
{
    static Runtime runtime;
    return runtime;
}

void Runtime::Start(Channel& channel, ErrorSink reportError)
{
    std::lock_guard lock(gate_);
    if (channel_ != nullptr) {
        throw std::logic_error("Game events are already started.");
    }
    auto adapters = Registrations();
    std::set<std::string> names;
    for (const auto& adapter : adapters) {
        if (adapter.name.empty() || !adapter.message || !adapter.stop || !names.insert(adapter.name).second) {
            throw std::logic_error("Invalid or duplicate game event adapter.");
        }
    }
    std::ranges::sort(adapters, [](const Adapter& left, const Adapter& right) {
        return std::tie(left.order, left.name) < std::tie(right.order, right.name);
    });
    adapters_ = std::move(adapters);
    reportError_ = std::move(reportError);
    channel_ = &channel;
    dataReady_ = false;
    accepting_ = true;
}

bool Runtime::Enter()
{
    std::lock_guard lock(gate_);
    if (!accepting_) {
        return false;
    }
    ++active_;
    return true;
}

void Runtime::Leave() noexcept
{
    std::lock_guard lock(gate_);
    if (--active_ == 0) {
        idle_.notify_all();
    }
}

void Runtime::Stop() noexcept
{
    {
        std::unique_lock lock(gate_);
        accepting_ = false;
        idle_.wait(lock, [&] { return active_ == 0; });
        if (channel_ == nullptr) {
            return;
        }
    }
    // UnregisterSink can wait for engine callbacks. Never hold gate_ while doing so.
    for (auto adapter = adapters_.rbegin(); adapter != adapters_.rend(); ++adapter) {
        try {
            adapter->stop();
        } catch (const std::exception& exception) {
            Report(exception.what());
        } catch (...) {
            Report("Game event adapter cleanup failed.");
        }
    }
    std::lock_guard lock(gate_);
    channel_ = nullptr;
    dataReady_ = false;
    adapters_.clear();
}

void Runtime::Dispatch(const Message& message) noexcept
{
    Invoke([&] {
        if (message.Is(MessageType::GameDataReady)) {
            // F4SE encodes this boolean in the pointer VALUE, not at that address.
            dataReady_ = message.data != nullptr;
        }
        for (const auto& adapter : adapters_) {
            try {
                adapter.message(*this, message);
            } catch (const std::exception& exception) {
                Report(exception.what());
            } catch (...) {
                Report("Game event adapter failed.");
            }
        }
    });
}

void Runtime::Publish(std::string_view name, const DataValue& value)
{
    channel_->Events().Publish(name, value);
}

void Runtime::Report(std::string_view message) const noexcept
{
    try {
        if (reportError_) {
            reportError_(message);
        }
    } catch (...) {
        // A failing logger must not escape an engine callback.
    }
}

}
