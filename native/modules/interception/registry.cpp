#include "registry.h"
#include <stdexcept>

namespace f4forge::interception {

Registry::Registry(Channel& channel, ErrorSink report) : channel_(channel), report_(std::move(report))
{
    handlers_.reserve(2);
    try {
        handlers_.push_back(channel_.Add("interceptors.register", [this](const DataValue& arguments) {
            const auto name = arguments.at("name").get<std::string>();
            const auto method = arguments.at("method").get<std::string>();
            if (name.empty() || name.size() > 256 || method.empty() || method.size() > 256) {
                throw std::invalid_argument("Interceptor name and callback method must contain 1 to 256 bytes.");
            }
            std::lock_guard lock(gate_);
            const auto id = next_++;
            entries_.emplace(id, std::make_shared<Entry>(name, method));
            return id;
        }));
        handlers_.push_back(channel_.Add("interceptors.remove", [this](const DataValue& arguments) {
            const auto id = arguments.at("registration").get<std::uint64_t>();
            std::lock_guard lock(gate_);
            const auto entry = entries_.find(id);
            if (entry != entries_.end()) {
                entry->second->active = false;
                entries_.erase(entry);
            }
            return true;
        }));
    } catch (...) {
        for (const auto handler : handlers_) {
            channel_.Remove(handler);
        }
        throw;
    }
}

Registry::~Registry()
{
    for (const auto handler : handlers_) {
        channel_.Remove(handler);
    }
}

DataValue Registry::Apply(std::string_view name, DataValue input, const Validator& validate)
{
    // Reuse the channel's dispatch lane for the entire chain. This is recursive
    // on the current thread and supplies the same removal barrier as messages.
    return channel_.Dispatch([&](const DataValue&) {
        std::vector<std::shared_ptr<Entry>> snapshot;
        {
            std::lock_guard lock(gate_);
            for (const auto& [id, entry] : entries_) {
                if (entry->name == name) {
                    snapshot.push_back(entry);
                }
            }
        }
        const auto& serializer = channel_.Serializer();
        for (const auto& entry : snapshot) {
            if (!entry->active) {
                continue;
            }
            try {
                const auto request = serializer.Serialize({{"method", entry->method}, {"arguments", input}});
                const auto response = serializer.Deserialize(channel_.Call(request));
                if (!response.at("success").get<bool>()) {
                    throw std::runtime_error(response.at("error").at("message").get<std::string>());
                }
                // Commit only a complete, valid decision. A failed handler cannot
                // erase a decision already accepted from an earlier interceptor.
                auto next = validate(input, response.at("value"));
                input = std::move(next);
                if (input.at("skip").get<bool>()) {
                    break;
                }
            } catch (const std::exception& exception) {
                if (report_) {
                    try {
                        report_("Interceptor '" + std::string(name) + "', callback '" + entry->method + "': " + exception.what());
                    } catch (...) {
                        // Error sinks must not alter the engine decision.
                    }
                }
            }
        }
        return input;
    });
}

}
