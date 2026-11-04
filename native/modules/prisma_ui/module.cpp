#include "../extensions/transport.h"
#include "PrismaUI_F4_API.h"
#include "components.h"
#include "cleanup.h"
#include <array>

namespace f4forge::prisma_ui
{
namespace
{
using Api = PRISMA_UI_API::IVPrismaUI2;
struct View
{
    PrismaView handle = 0;
    std::string scope;
    bool ready = false;
    std::size_t slot = 0;
};

void RegisterListener(const View& view);

Api* api = nullptr;
std::atomic<Channel*> channel = nullptr;
std::vector<std::uint64_t> routes;
std::map<std::string, std::shared_ptr<View>> views;

// Prisma's callback ABI has no userdata. Stable, non-reused slots prevent a
// callback queued by a destroyed view from reaching a subsequent owner.
constexpr std::size_t MaxViews = 256;
std::array<std::weak_ptr<View>, MaxViews> slots;
std::mutex slotsGate;
std::size_t nextSlot = 0;

template <std::size_t Index> void Message(const char* value) noexcept
{
    try
    {
        std::shared_ptr<View> view;
        {
            std::scoped_lock lock(slotsGate);
            view = slots[Index].lock();
        }
        if (!view)
        {
            return;
        }
        const std::string copy = value != nullptr ? value : "";
        extensions::Queue([weak = std::weak_ptr(view), copy] {
            const auto current = weak.lock();
            if (!current)
            {
                return;
            }
            game_events::Host().Publish("prisma.message", {{"scope", current->scope}, {"message", copy}});
        });
    }
    catch (...)
    {
        game_events::Host().Report("Cannot deliver a Prisma UI message.");
    }
}

void Ready(PrismaView handle) noexcept
{
    try
    {
        extensions::Queue([handle] {
            auto* owner = channel.load();
            if (owner == nullptr || api == nullptr)
            {
                return;
            }
            owner->Dispatch([handle](const DataValue&) -> DataValue {
                for (const auto& [scope, view] : views)
                {
                    if (view->handle != handle)
                    {
                        continue;
                    }
                    RegisterListener(*view);
                    view->ready = true;
                    game_events::Host().Publish("prisma.ready", {{"scope", scope}});
                    break;
                }
                return nullptr;
            });
        });
    }
    catch (...)
    {
        game_events::Host().Report("Cannot deliver Prisma UI readiness.");
    }
}

template <std::size_t... Indices> constexpr auto Callbacks(std::index_sequence<Indices...>)
{
    return std::array<PRISMA_UI_API::JSListenerCallback, sizeof...(Indices)>{&Message<Indices>...};
}
constexpr auto callbacks = Callbacks(std::make_index_sequence<MaxViews>{});

void RegisterListener(const View& view)
{
    api->RegisterJSListener(view.handle, "f4forge", callbacks[view.slot]);
}

void Destroy(std::shared_ptr<View> view)
{
    // Forget the callback's weak target before scheduling engine cleanup.
    const auto handle = view->handle;
    const auto* tasks = F4SE::GetTaskInterface();
    if (tasks == nullptr)
    {
        throw std::runtime_error("F4SE task interface is unavailable.");
    }
    QueueDestruction(api, handle, [tasks](auto cleanup) {
        tasks->AddTask(std::move(cleanup));
    });
}

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    routes.push_back(owner.Add("prisma.available", [](const DataValue&) -> DataValue {
        extensions::RequireGameThread();
        api = PRISMA_UI_API::RequestPluginAPI<Api>();
        return api != nullptr;
    }));
    routes.push_back(owner.Add("prisma.create", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        api = PRISMA_UI_API::RequestPluginAPI<Api>();
        if (api == nullptr)
        {
            throw std::runtime_error("PrismaUI_F4 API v2 is unavailable.");
        }
        const auto scope = request.at("scope").get<std::string>();
        const auto path = request.at("path").get<std::string>();
        if (scope.empty() || path.empty() || path.find("..") != std::string::npos ||
            path.find(':') != std::string::npos)
        {
            throw std::invalid_argument("Expected an owned scope and a relative Prisma view path.");
        }
        if (views.contains(scope))
        {
            throw std::invalid_argument("View scope already exists.");
        }
        std::size_t slot;
        {
            std::scoped_lock lock(slotsGate);
            if (nextSlot == MaxViews)
            {
                throw std::runtime_error("Prisma callback capacity exhausted; restart the game.");
            }
            slot = nextSlot++;
        }
        auto view = std::make_shared<View>();
        view->scope = scope;
        view->slot = slot;
        view->handle = api->CreateView(path.c_str(), Ready);
        if (view->handle == 0)
        {
            throw std::runtime_error("Prisma UI could not create the view.");
        }
        api->Hide(view->handle);
        views.emplace(scope, view);
        {
            std::scoped_lock lock(slotsGate);
            slots[slot] = view;
        }
        return true;
    }));
    routes.push_back(owner.Add("prisma.update", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        const auto found = views.find(request.at("scope").get<std::string>());
        if (found == views.end() || api == nullptr)
        {
            throw std::runtime_error("Prisma view has closed.");
        }
        const auto handle = found->second->handle;
        const auto operation = request.at("operation").get<std::string>();
        if (operation == "show")
        {
            api->Show(handle);
            if (!api->Focus(handle, true))
            {
                api->Hide(handle);
                throw std::runtime_error("Prisma UI refused focus.");
            }
        }
        else if (operation == "hide")
        {
            api->Unfocus(handle);
            api->Hide(handle);
        }
        else if (operation == "send")
        {
            if (!found->second->ready)
            {
                throw std::runtime_error("Prisma DOM is not ready.");
            }
            const auto message = request.at("message").get<std::string>();
            api->InteropCall(handle, "f4forgeReceive", message.c_str());
        }
        else if (operation == "ready")
        {
            return found->second->ready;
        }
        else
        {
            throw std::invalid_argument("Unknown Prisma view operation.");
        }
        return true;
    }));
    routes.push_back(owner.Add("prisma.release", [](const DataValue& request) -> DataValue {
        const auto found = views.find(request.at("scope").get<std::string>());
        if (found != views.end())
        {
            auto view = std::move(found->second);
            views.erase(found);
            Destroy(std::move(view));
        }
        return true;
    }));
}

void Stop()
{
    auto* owner = channel.exchange(nullptr);
    if (owner == nullptr)
    {
        return;
    }
    owner->Dispatch([](const DataValue&) -> DataValue {
        for (auto& [scope, view] : views)
        {
            Destroy(std::move(view));
        }
        views.clear();
        return nullptr;
    });
    for (const auto route : routes)
    {
        owner->Remove(route);
    }
    routes.clear();
}

const ComponentRegistration component({"prisma.ui", Start, Stop});
} // namespace
} // namespace f4forge::prisma_ui
