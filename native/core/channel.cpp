#include "channel.h"

#include <stdexcept>
#include <utility>

namespace f4forge {
namespace {

std::string Error(std::string_view code, std::string_view message)
{
    return Json{{"error", {{"code", code}, {"message", message.substr(0, 4096)}}}}.dump(-1, ' ', false, Json::error_handler_t::replace);
}

int CopyResponse(void* context, const char* data, std::uint32_t size) noexcept
{
    try {
        if (data == nullptr || size > Channel::MaxMessageBytes) {
            return 1;
        }
        *static_cast<std::string*>(context) = std::string(data, size);
        return 0;
    } catch (...) {
        return 1;
    }
}

int Call(void* context, const char* data, std::uint32_t size, Receive receive, void* receiver) noexcept
{
    try {
        if (context == nullptr || data == nullptr || receive == nullptr || size > Channel::MaxMessageBytes) {
            return 1;
        }
        const auto response = static_cast<Channel*>(context)->Call({data, size});
        return receive(receiver, response.data(), static_cast<std::uint32_t>(response.size()));
    } catch (...) {
        return 1;
    }
}

std::uint64_t Add(void* context, const char* name, std::uint32_t size, Invoke invoke, void* handler) noexcept
{
    try {
        if (context == nullptr || name == nullptr || size == 0 || size > 256 || invoke == nullptr) {
            return 0;
        }
        return static_cast<Channel*>(context)->Add(std::string(name, size), [invoke, handler](const Json& arguments) {
            const auto payload = arguments.dump();
            std::string response;
            if (invoke(handler, payload.data(), static_cast<std::uint32_t>(payload.size()), &CopyResponse, &response) != 0) {
                throw std::runtime_error("SDK callback failed.");
            }
            const auto envelope = Json::parse(response);
            if (envelope.contains("error")) {
                throw std::runtime_error(envelope.at("error").at("message").get<std::string>());
            }
            return envelope.at("value");
        });
    } catch (...) {
        return 0;
    }
}

void Remove(void* context, std::uint64_t registration) noexcept
{
    if (context != nullptr) {
        static_cast<Channel*>(context)->Remove(registration);
    }
}

}

std::uint64_t Channel::Add(std::string name, Handler handler)
{
    if (name.empty() || name.size() > 256 || !handler) {
        throw std::invalid_argument("A method name and handler are required.");
    }
    std::lock_guard lock(mutex_);
    if (handlers_.contains(name)) {
        throw std::invalid_argument("Method already registered: " + name);
    }
    const auto registration = nextRegistration_++;
    handlers_.emplace(std::move(name), Entry{registration, std::move(handler)});
    return registration;
}

void Channel::Remove(std::uint64_t registration)
{
    std::lock_guard lock(mutex_);
    std::erase_if(handlers_, [registration](const auto& item) {
        return item.second.registration == registration;
    });
}

std::string Channel::Call(std::string_view request)
{
    if (request.size() > MaxMessageBytes) {
        return Error("invalid_request", "Message exceeds the size limit.");
    }
    const auto envelope = Json::parse(request, nullptr, false);
    if (!envelope.is_object() || !envelope.contains("method") || !envelope["method"].is_string()
        || !envelope.contains("arguments")) {
        return Error("invalid_request", "Expected method and arguments.");
    }
    std::lock_guard lock(mutex_);
    const auto found = handlers_.find(envelope["method"].get<std::string>());
    if (found == handlers_.end()) {
        return Error("method_not_found", "No handler is registered for this method.");
    }
    try {
        // Copy before invoking: a nested call may unregister this entry.
        const auto handler = found->second.handler;
        const auto response = Json{{"value", handler(envelope["arguments"])}}.dump();
        if (response.size() > MaxMessageBytes) {
            return Error("response_too_large", "Response exceeds the size limit.");
        }
        return response;
    } catch (const std::exception& exception) {
        return Error("handler_failed", exception.what());
    } catch (...) {
        return Error("handler_failed", "Unknown handler failure.");
    }
}

ChannelBinding Channel::Binding() noexcept
{
    return {this, &f4forge::Call, &f4forge::Add, &f4forge::Remove};
}

}
