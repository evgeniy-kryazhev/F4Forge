#include "service.h"

namespace f4forge::papyrus
{
namespace
{

std::string Identifier(const DataValue& value, std::string_view field, std::size_t maximum = 128)
{
    const auto text = value.at(field).get<std::string>();
    if (text.empty() || text.size() > maximum || text.find('\0') != std::string::npos)
    {
        throw std::invalid_argument("Invalid Papyrus identifier: " + std::string(field));
    }
    return text;
}

} // namespace

struct Service::State
{
    std::mutex gate;
    bool closed = false;
    std::uint64_t epoch = 1;
    std::map<std::string, std::set<std::string>, std::less<>> pending;
    std::set<std::string> released;
    Backend backend;
    Publish publish;

    bool Contains(const std::string& scope, const std::string& id, std::uint64_t expectedEpoch)
    {
        std::scoped_lock lock(gate);
        const auto found = pending.find(scope);
        return !closed && epoch == expectedEpoch && found != pending.end() && found->second.contains(id);
    }

    void Finish(const std::string& scope, const std::string& id, std::uint64_t expectedEpoch, DataValue value,
                std::string error)
    {
        {
            std::scoped_lock lock(gate);
            const auto found = pending.find(scope);
            if (closed || epoch != expectedEpoch || found == pending.end() || found->second.erase(id) == 0)
            {
                return;
            }
        }
        // Publication may call mod code. No registry lock spans this callback.
        publish(
            {{"requestId", id}, {"success", error.empty()}, {"value", std::move(value)}, {"error", std::move(error)}});
    }
};

Service::Service(Channel& channel, Backend backend, Publish publish)
    : channel_(channel), state_(std::make_shared<State>())
{
    state_->backend = std::move(backend);
    state_->publish = std::move(publish);
    const auto state = state_;
    registrations_.reserve(3);
    try
    {
        registrations_.push_back(channel.Add("papyrus.begin", [state](const DataValue& request) {
            const auto scope = Identifier(request, "scope");
            const auto id = Identifier(request, "requestId");
            Identifier(request, "scriptName", 256);
            Identifier(request, "name", 256);
            const auto operation = request.at("operation").get<std::string>();
            if (operation != "static" && operation != "method" && operation != "get" && operation != "set")
            {
                throw std::invalid_argument("Unknown Papyrus operation.");
            }
            if (!request.at("arguments").is_array() || request.at("arguments").size() > 128)
            {
                throw std::invalid_argument("Papyrus arguments must be an array of at most 128 values.");
            }
            std::uint64_t epoch;
            {
                std::scoped_lock lock(state->gate);
                if (state->closed || state->released.contains(scope) || !state->pending[scope].insert(id).second)
                {
                    throw std::runtime_error("Papyrus request is closed or already registered.");
                }
                epoch = state->epoch;
            }
            try
            {
                state->backend.submit(
                    request,
                    [state, scope, id, epoch](DataValue value, std::string error) {
                        state->Finish(scope, id, epoch, std::move(value), std::move(error));
                    },
                    [state, scope, id, epoch] { return state->Contains(scope, id, epoch); });
            }
            catch (...)
            {
                std::scoped_lock lock(state->gate);
                state->pending[scope].erase(id);
                throw;
            }
            return true;
        }));
        registrations_.push_back(channel.Add("papyrus.forget", [state](const DataValue& request) {
            const auto scope = Identifier(request, "scope");
            const auto id = Identifier(request, "requestId");
            std::scoped_lock lock(state->gate);
            const auto found = state->pending.find(scope);
            return found != state->pending.end() && found->second.erase(id) != 0;
        }));
        registrations_.push_back(channel.Add("papyrus.release", [state](const DataValue& request) {
            const auto scope = Identifier(request, "scope");
            {
                std::scoped_lock lock(state->gate);
                state->pending.erase(scope);
                // A release can race a managed submission before it enters the
                // channel. A retired scope must never be reopened by that call.
                state->released.insert(scope);
            }
            state->backend.release(scope);
            return true;
        }));
    }
    catch (...)
    {
        for (const auto registration : registrations_)
        {
            channel.Remove(registration);
        }
        throw;
    }
}

Service::~Service()
{
    Stop();
}

void Service::Stop()
{
    {
        std::scoped_lock lock(state_->gate);
        if (state_->closed)
        {
            return;
        }
        state_->closed = true;
        state_->pending.clear();
    }
    for (const auto registration : registrations_)
    {
        channel_.Remove(registration);
    }
    state_->backend.reset();
}

void Service::Reset()
{
    std::map<std::string, std::set<std::string>, std::less<>> pending;
    {
        std::scoped_lock lock(state_->gate);
        if (state_->closed)
        {
            return;
        }
        pending.swap(state_->pending);
        ++state_->epoch;
    }
    state_->backend.reset();
    for (const auto& [scope, requests] : pending)
    {
        (void)scope;
        for (const auto& id : requests)
        {
            state_->publish({{"requestId", id},
                             {"success", false},
                             {"value", nullptr},
                             {"error", "The game session changed before Papyrus completed."}});
        }
    }
}

} // namespace f4forge::papyrus
