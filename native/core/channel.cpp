#include "channel.h"

#include <algorithm>
#include <stdexcept>
#include <utility>

namespace f4forge {
namespace {

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

int Dispatch(void* context, const char* data, std::uint32_t size, Receive receive, void* receiver, bool publish) noexcept
{
    try {
        if (context == nullptr || data == nullptr || receive == nullptr || size > Channel::MaxMessageBytes) {
            return 1;
        }
        auto& channel = *static_cast<Channel*>(context);
        const auto response = publish ? channel.Publish({data, size}) : channel.Call({data, size});
        return receive(receiver, response.data(), static_cast<std::uint32_t>(response.size()));
    } catch (...) {
        return 1;
    }
}

int Call(void* context, const char* data, std::uint32_t size, Receive receive, void* receiver) noexcept
{
    return Dispatch(context, data, size, receive, receiver, false);
}

int Publish(void* context, const char* data, std::uint32_t size, Receive receive, void* receiver) noexcept
{
    return Dispatch(context, data, size, receive, receiver, true);
}

Channel::Handler WrapHandler(Channel& channel, Invoke invoke, void* handler)
{
    return [&channel, invoke, handler](const DataValue& arguments) {
        const auto& serializer = channel.Serializer();
        const auto payload = serializer.Serialize(arguments);
        if (payload.size() > Channel::MaxMessageBytes) {
            throw std::length_error("Callback data exceeds the size limit.");
        }
        std::string response;
        if (invoke(handler, payload.data(), static_cast<std::uint32_t>(payload.size()), &CopyResponse, &response) != 0) {
            throw std::runtime_error("SDK callback failed.");
        }
        const auto envelope = serializer.Deserialize(response);
        if (!envelope.at("success").get<bool>()) {
            throw std::runtime_error(envelope.at("error").at("message").get<std::string>());
        }
        return envelope.at("value");
    };
}

std::uint64_t Register(void* context, const char* name, std::uint32_t size, Invoke invoke, void* handler, bool subscribe) noexcept
{
    try {
        if (context == nullptr || name == nullptr || size == 0 || size > 256 || invoke == nullptr) {
            return 0;
        }
        auto& channel = *static_cast<Channel*>(context);
        auto callback = WrapHandler(channel, invoke, handler);
        if (subscribe) {
            return channel.Events().Subscribe(std::string(name, size), [callback = std::move(callback)](const DataValue& data) {
                callback(data);
            });
        }
        return channel.Add(std::string(name, size), std::move(callback));
    } catch (...) {
        return 0;
    }
}

std::uint64_t Add(void* context, const char* name, std::uint32_t size, Invoke invoke, void* handler) noexcept
{
    return Register(context, name, size, invoke, handler, false);
}

std::uint64_t Subscribe(void* context, const char* name, std::uint32_t size, Invoke invoke, void* handler) noexcept
{
    return Register(context, name, size, invoke, handler, true);
}

void Remove(void* context, std::uint64_t registration) noexcept
{
    if (context != nullptr) {
        static_cast<Channel*>(context)->Remove(registration);
    }
}

void Unsubscribe(void* context, std::uint64_t registration) noexcept
{
    if (context != nullptr) {
        static_cast<Channel*>(context)->Events().Unsubscribe(registration);
    }
}

}

Channel::Channel(std::shared_ptr<const IDataSerializer> serializer)
    : serializer_(std::move(serializer)), events_(dispatchMutex_)
{
    if (!serializer_ || serializer_->Format().empty()) {
        throw std::invalid_argument("A data serializer with a format identifier is required.");
    }
    dataFormat_ = serializer_->Format();
}

const IDataSerializer& Channel::Serializer() const noexcept
{
    return *serializer_;
}

EventChannel& Channel::Events() noexcept
{
    return events_;
}

std::string Channel::Error(std::string_view code, std::string_view message) const
{
    const auto response = serializer_->Serialize(DataValue{{"success", false}, {"error", {{"code", code}, {"message", message.substr(0, 4096)}}}});
    if (response.size() > MaxMessageBytes) {
        throw std::length_error("Serialized error exceeds the size limit.");
    }
    return response;
}

std::string Channel::Response(const DataValue& value) const
{
    const auto response = serializer_->Serialize(DataValue{{"success", true}, {"value", value}});
    if (response.size() > MaxMessageBytes) {
        return Error("response_too_large", "Response exceeds the size limit.");
    }
    return response;
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
    Handler removed;
    {
        std::lock_guard registryLock(mutex_);
        const auto found = std::ranges::find_if(handlers_, [registration](const auto& item) {
            return item.second.registration == registration;
        });
        if (found != handlers_.end()) {
            removed = std::move(found->second.handler);
            handlers_.erase(found);
        }
    }
    std::lock_guard dispatchLock(dispatchMutex_);
}

DataValue Channel::Dispatch(const Handler& action)
{
    std::lock_guard dispatchLock(dispatchMutex_);
    return action(nullptr);
}

std::string Channel::Call(std::string_view request)
{
    if (request.size() > MaxMessageBytes) {
        return Error("invalid_request", "Message exceeds the size limit.");
    }
    DataValue envelope;
    try {
        envelope = serializer_->Deserialize(request);
    } catch (const std::exception&) {
        return Error("invalid_request", "Cannot deserialize message.");
    }
    if (!envelope.is_object() || !envelope.contains("method") || !envelope["method"].is_string()
        || !envelope.contains("arguments")) {
        return Error("invalid_request", "Expected method and arguments.");
    }
    std::lock_guard dispatchLock(dispatchMutex_);
    Handler handler;
    {
        std::lock_guard registryLock(mutex_);
        const auto found = handlers_.find(envelope["method"].get<std::string>());
        if (found != handlers_.end()) {
            handler = found->second.handler;
        }
    }
    if (!handler) {
        return Error("method_not_found", "No handler is registered for this method.");
    }
    try {
        return Response(handler(envelope["arguments"]));
    } catch (const std::exception& exception) {
        return Error("handler_failed", exception.what());
    } catch (...) {
        return Error("handler_failed", "Unknown handler failure.");
    }
}

std::string Channel::Publish(std::string_view request)
{
    if (request.size() > MaxMessageBytes) {
        return Error("invalid_request", "Event exceeds the size limit.");
    }
    DataValue envelope;
    try {
        envelope = serializer_->Deserialize(request);
    } catch (const std::exception&) {
        return Error("invalid_request", "Cannot deserialize event.");
    }
    if (!envelope.is_object() || !envelope.contains("event") || !envelope["event"].is_string()
        || !envelope.contains("data")) {
        return Error("invalid_request", "Expected event and data.");
    }
    const auto name = envelope["event"].get<std::string>();
    if (name.empty() || name.size() > 256) {
        return Error("invalid_request", "An event name is required.");
    }
    events_.Publish(name, envelope["data"]);
    return Response(nullptr);
}

ChannelBinding Channel::Binding() noexcept
{
    return {this, &f4forge::Call, &f4forge::Add, &f4forge::Remove,
        &f4forge::Publish, &f4forge::Subscribe, &f4forge::Unsubscribe, dataFormat_.c_str()};
}

}
