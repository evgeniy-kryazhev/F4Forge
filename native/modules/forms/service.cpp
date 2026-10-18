#include "service.h"
#include <cmath>
#include <limits>
#include <stdexcept>

namespace f4forge::forms {

namespace {

std::uint64_t Unsigned(const DataValue& arguments, const char* name, std::uint64_t maximum)
{
    const auto& value = arguments.at(name);
    if (!value.is_number_integer() || (!value.is_number_unsigned() && value.get<std::int64_t>() < 0)) {
        throw std::invalid_argument(std::string(name) + " must be an unsigned integer.");
    }
    const auto result = value.get<std::uint64_t>();
    if (result > maximum) {
        throw std::invalid_argument(std::string(name) + " is out of range.");
    }
    return result;
}

std::uint32_t FormId(const DataValue& arguments, const char* name = "formId")
{
    return static_cast<std::uint32_t>(Unsigned(arguments, name, std::numeric_limits<std::uint32_t>::max()));
}

std::uint64_t Generation(const DataValue& arguments, const char* name = "session")
{
    const auto generation = Unsigned(arguments, name, std::numeric_limits<std::uint64_t>::max());
    if (generation == 0) {
        throw std::invalid_argument("A resolved object's nonzero session is required.");
    }
    return generation;
}

}

void Session::Ready(bool ready)
{
    std::lock_guard lock(gate_);
    if (ready != ready_) {
        ++generation_;
    }
    ready_ = ready;
    if (ready) {
        thread_ = std::this_thread::get_id();
    }
}

void Session::Reset()
{
    std::lock_guard lock(gate_);
    ++generation_;
}

std::uint64_t Session::Require(std::uint64_t expected) const
{
    std::lock_guard lock(gate_);
    if (!ready_) {
        throw std::runtime_error("Game objects are unavailable until game data is ready and loading has finished.");
    }
    if (thread_ != std::this_thread::get_id()) {
        throw std::runtime_error("Game object operations must execute in the game thread.");
    }
    if (expected != 0 && expected != generation_) {
        throw std::runtime_error("Game object belongs to an expired game session. Resolve it again.");
    }
    return generation_;
}

Service::Service(Channel& channel, Session& session, Backend backend)
    : channel_(channel), session_(session), backend_(std::move(backend))
{
    if (!backend_.describe || !backend_.resolve || !backend_.player || !backend_.reference || !backend_.actorValue || !backend_.setBaseValue) {
        throw std::invalid_argument("A complete game object backend is required.");
    }
    // Allocate before publishing callbacks so tracking cannot fail after Add().
    registrations_.reserve(6);
    try {
        Add("forms.describe", [this](const DataValue& arguments) {
            const auto generation = session_.Require(arguments.contains("session") ? Generation(arguments) : 0);
            return Describe(backend_.describe(FormId(arguments)), generation);
        });
        Add("forms.resolve", [this](const DataValue& arguments) {
            const auto generation = session_.Require();
            if (!arguments.contains("editorId")) {
                Unsigned(arguments, "localId", 0xffffff);
            }
            return Describe(backend_.resolve(arguments), generation);
        });
        Add("forms.player", [this](const DataValue&) {
            const auto generation = session_.Require();
            return Describe(backend_.player(), generation);
        });
        Add("forms.reference", [this](const DataValue& arguments) {
            session_.Require(Generation(arguments));
            return backend_.reference(FormId(arguments));
        });
        Add("forms.actor_value", [this](const DataValue& arguments) {
            session_.Require(Generation(arguments));
            session_.Require(Generation(arguments, "actorValueSession"));
            const auto kind = arguments.at("kind").get<std::string>();
            ValueKind valueKind;
            if (kind == "current") {
                valueKind = ValueKind::Current;
            } else if (kind == "base") {
                valueKind = ValueKind::Base;
            } else if (kind == "permanent") {
                valueKind = ValueKind::Permanent;
            } else {
                throw std::invalid_argument("Unsupported actor value kind.");
            }
            return backend_.actorValue(FormId(arguments), FormId(arguments, "actorValueFormId"), valueKind);
        });
        Add("forms.set_base_value", [this](const DataValue& arguments) {
            session_.Require(Generation(arguments));
            session_.Require(Generation(arguments, "actorValueSession"));
            const auto value = arguments.at("value").get<float>();
            if (!std::isfinite(value)) {
                throw std::invalid_argument("Actor value must be finite.");
            }
            backend_.setBaseValue(FormId(arguments), FormId(arguments, "actorValueFormId"), value);
            return true;
        });
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

void Service::Add(std::string name, Channel::Handler handler)
{
    registrations_.push_back(channel_.Add(std::move(name), std::move(handler)));
}

DataValue Service::Describe(DataValue value, std::uint64_t generation) const
{
    if (!value.is_null()) {
        value["session"] = generation;
    }
    return value;
}

}
