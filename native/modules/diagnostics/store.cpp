#include "store.h"
#include <algorithm>
#include <chrono>
#include <cmath>
#include <stdexcept>

namespace f4forge::diagnostics {
namespace {

std::string Text(const DataValue& value, const char* name, std::size_t maximum)
{
    const auto text = value.at(name).get<std::string>();
    if (text.size() > maximum) {
        throw std::invalid_argument(std::string("Diagnostic field is too long: ") + name);
    }
    return text;
}

}

Store& Global()
{
    static Store store;
    return store;
}

void Store::Record(const DataValue& operation)
{
    const auto modId = Text(operation, "modId", 4096);
    const auto name = Text(operation, "name", 256);
    Text(operation, "id", 64);
    Text(operation, "parentId", 64);
    Text(operation, "kind", 64);
    Text(operation, "exceptionType", 1024);
    const auto message = Text(operation, "message", 8192);
    Text(operation, "stackTrace", 32768);
    const auto elapsed = operation.at("elapsedMilliseconds").get<double>();
    const auto success = operation.at("success").get<bool>();
    if (modId.empty() || name.empty() || !std::isfinite(elapsed) || elapsed < 0) {
        throw std::invalid_argument("Invalid diagnostic operation.");
    }
    {
        std::lock_guard lock(gate_);
        if (operations_.size() == Capacity) {
            operations_.pop_front();
        }
        operations_.push_back(operation);
    }
    if (!success) {
        Report("Mod '" + modId + "', " + name + ": " + message + "\n" + operation.at("stackTrace").get<std::string>());
    }
}

DataValue Store::Operations(std::string_view modId, std::size_t maximum) const
{
    if (maximum == 0 || maximum > Capacity) {
        throw std::invalid_argument("Diagnostic limit must be between 1 and 256.");
    }
    std::lock_guard lock(gate_);
    auto result = DataValue::array();
    for (auto entry = operations_.rbegin(); entry != operations_.rend() && result.size() < maximum; ++entry) {
        if (modId.empty() || entry->at("modId").get_ref<const std::string&>() == modId) {
            result.push_back(*entry);
        }
    }
    return result;
}

void Store::Hook(std::string name, std::string kind, std::string state, std::string detail)
{
    const auto now = std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()).count();
    const auto failure = state == "failed";
    {
        std::lock_guard lock(gate_);
        hooks_[name] = {{"name", name}, {"kind", kind}, {"state", state},
            {"detail", detail}, {"updatedAtUnixMilliseconds", now}};
    }
    if (failure) {
        Report("Hook '" + name + "': " + detail);
    }
}

DataValue Store::Hooks() const
{
    std::lock_guard lock(gate_);
    auto result = DataValue::array();
    for (const auto& [name, status] : hooks_) {
        result.push_back(status);
    }
    return result;
}

void Store::SetErrorSink(ErrorSink sink)
{
    std::lock_guard lock(gate_);
    errorSink_ = std::move(sink);
}

void Store::Report(std::string_view text) const noexcept
{
    try {
        ErrorSink sink;
        {
            std::lock_guard lock(gate_);
            sink = errorSink_;
        }
        if (sink) {
            sink(text);
        }
    } catch (...) {
        // Diagnostics must not turn a recoverable mod error into a host failure.
    }
}

Service::Service(Channel& channel, Store& store) : channel_(channel)
{
    // Allocate before publishing callbacks so tracking cannot fail after Add().
    registrations_.reserve(3);
    try {
        registrations_.push_back(channel_.Add("diagnostics.record", [&store](const DataValue& value) {
            store.Record(value);
            return true;
        }));
        registrations_.push_back(channel_.Add("diagnostics.operations", [&store, &channel](const DataValue& value) {
            auto result = store.Operations(value.value("modId", std::string{}), value.value("maximum", std::size_t{64}));
            // Fit the complete envelope using the actual configured codec. Large
            // stack traces must not make the history endpoint unusable.
            while (!result.empty() && channel.Serializer().Serialize({{"success", true}, {"value", result}}).size() > Channel::MaxMessageBytes) {
                result.erase(result.size() - 1);
            }
            return result;
        }));
        registrations_.push_back(channel_.Add("diagnostics.hooks", [&store](const DataValue&) {
            return store.Hooks();
        }));
    } catch (...) {
        for (const auto registration : registrations_) {
            channel_.Remove(registration);
        }
        throw;
    }
}

Service::~Service()
{
    for (const auto registration : registrations_) {
        channel_.Remove(registration);
    }
}

}
