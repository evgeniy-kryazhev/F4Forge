#include "../extensions/transport.h"
#include "components.h"
#include <Windows.h>
#include <cmath>

namespace f4forge::game_data
{
namespace
{
Channel* channel = nullptr;
std::vector<std::uint64_t> routes;

RE::TESForm* Resolve(const DataValue& request)
{
    const auto plugin = request.at("plugin").get<std::string>();
    const auto id = request.at("localId").get<std::uint32_t>();
    if (plugin.empty())
    {
        return RE::TESForm::GetFormByID(id);
    }
    auto* data = RE::TESDataHandler::GetSingleton();
    return data != nullptr ? data->LookupForm(id, plugin) : nullptr;
}

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    routes.push_back(owner.Add("runtime.native_plugin_loaded", [](const DataValue& request) -> DataValue {
        const auto name = request.at("name").get<std::string>();
        if (name.empty() || name.find_first_of("/\\:") != std::string::npos)
        {
            throw std::invalid_argument("Expected a native plugin filename.");
        }
        const std::u8string utf8Name(name.begin(), name.end());
        return GetModuleHandleW(std::filesystem::path(utf8Name).c_str()) != nullptr;
    }));
    routes.push_back(owner.Add("runtime.data_ready", [](const DataValue&) -> DataValue {
        return game_events::Host().DataReady();
    }));
    routes.push_back(owner.Add("forms.plugin_active", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        const auto name = request.at("name").get<std::string>();
        auto* data = RE::TESDataHandler::GetSingleton();
        return data != nullptr &&
               (data->LookupLoadedModByName(name) != nullptr || data->LookupLoadedLightModByName(name) != nullptr);
    }));
    routes.push_back(owner.Add("forms.description", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        auto* form = Resolve(request);
        auto* description = form != nullptr ? form->As<RE::TESDescription>() : nullptr;
        if (description == nullptr)
        {
            return "";
        }
        RE::BSString text;
        description->GetDescription(text, form);
        return std::string(text.c_str());
    }));
    routes.push_back(owner.Add("forms.global.read", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        auto* form = Resolve(request);
        auto* global = form != nullptr ? form->As<RE::TESGlobal>() : nullptr;
        if (global == nullptr)
        {
            throw std::invalid_argument("Global variable was not found.");
        }
        return global->value;
    }));
    routes.push_back(owner.Add("forms.global.write", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        const auto value = request.at("value").get<float>();
        if (!std::isfinite(value))
        {
            throw std::invalid_argument("Global values must be finite.");
        }
        auto* form = Resolve(request);
        auto* global = form != nullptr ? form->As<RE::TESGlobal>() : nullptr;
        if (global == nullptr)
        {
            throw std::invalid_argument("Global variable was not found.");
        }
        global->value = value;
        return true;
    }));
}

void Stop()
{
    if (channel == nullptr)
    {
        return;
    }
    for (const auto route : routes)
    {
        channel->Remove(route);
    }
    routes.clear();
    channel = nullptr;
}

const ComponentRegistration component({"game.data.extensions", Start, Stop});
} // namespace
} // namespace f4forge::game_data
