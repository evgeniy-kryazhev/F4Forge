#include "service.h"
#include "components.h"
#include "../game_events/runtime.h"
#include <RE/Fallout.h>

namespace f4forge::forms {
namespace {

Session& World()
{
    static Session session([] {
        // Load notifications may run on a loader thread. Only the engine's
        // main-thread identity authorizes synchronous access to game objects.
        const auto* main = RE::Main::GetSingleton();
        return main != nullptr && main->threadID != 0 && main->threadID == REX::W32::GetCurrentThreadId();
    });
    return session;
}

std::unique_ptr<Service>& ActiveService()
{
    static std::unique_ptr<Service> service;
    return service;
}

template<class Read>
auto WithForm(std::uint32_t id, Read&& read)
{
    auto* form = id != 0 ? RE::TESForm::GetFormByID(id) : nullptr;
    if (form == nullptr) {
        throw std::runtime_error("Game form no longer exists: " + std::to_string(id));
    }
    // References are ref-counted engine objects. Keep one native strong reference
    // during the operation; SDK wrappers never own or serialize a native pointer.
    const RE::NiPointer<RE::TESObjectREFR> reference(form->As<RE::TESObjectREFR>());
    return read(*form);
}

DataValue Describe(RE::TESForm* form)
{
    if (form == nullptr) {
        return nullptr;
    }
    const RE::NiPointer<RE::TESObjectREFR> reference(form->As<RE::TESObjectREFR>());
    const auto* editorId = form->GetFormEditorID();
    const auto* type = form->GetFormTypeString();
    return {{"formId", form->GetFormID()}, {"type", type != nullptr ? type : ""},
        {"name", std::string(RE::TESFullName::GetFullName(*form))},
        {"editorId", editorId != nullptr ? editorId : ""},
        {"isReference", reference != nullptr}, {"isActor", form->As<RE::Actor>() != nullptr},
        {"isActorValue", form->As<RE::ActorValueInfo>() != nullptr}};
}

RE::ActorValueInfo& ActorValue(std::uint32_t id)
{
    auto* value = RE::TESForm::GetFormByID<RE::ActorValueInfo>(id);
    if (value == nullptr) {
        throw std::invalid_argument("The supplied form is not an actor value definition.");
    }
    return *value;
}

RE::Actor& Actor(RE::TESForm& form)
{
    auto* actor = form.As<RE::Actor>();
    if (actor == nullptr) {
        throw std::invalid_argument("The supplied form is not an actor.");
    }
    return *actor;
}

Backend Engine()
{
    Backend backend;
    backend.describe = [](std::uint32_t id) {
        return Describe(id != 0 ? RE::TESForm::GetFormByID(id) : nullptr);
    };
    backend.resolve = [](const DataValue& arguments) {
        if (arguments.contains("editorId")) {
            const auto editorId = arguments.at("editorId").get<std::string>();
            if (editorId.empty() || editorId.size() > 256 || editorId.find('\0') != std::string::npos) {
                throw std::invalid_argument("A valid editor ID is required.");
            }
            return Describe(RE::TESForm::GetFormByEditorID(RE::BSFixedString(editorId)));
        }
        const auto plugin = arguments.at("plugin").get<std::string>();
        const auto localId = arguments.at("localId").get<std::uint32_t>();
        if (plugin.empty() || plugin.size() > 260 || plugin.find_first_of("/\\\r\n") != std::string::npos
            || plugin.find('\0') != std::string::npos || localId > 0xffffff) {
            throw std::invalid_argument("A plugin filename and local form ID are required.");
        }
        auto* data = RE::TESDataHandler::GetSingleton();
        if (data == nullptr) {
            return DataValue(nullptr);
        }
        const auto* file = data->LookupModByName(plugin);
        if (file == nullptr) {
            return DataValue(nullptr);
        }
        if (file->IsLight() && localId > 0xfff) {
            throw std::invalid_argument("A light plugin local form ID must fit in 12 bits.");
        }
        return Describe(data->LookupForm(localId, plugin));
    };
    backend.player = [] { return Describe(RE::PlayerCharacter::GetSingleton()); };
    backend.reference = [](std::uint32_t id) {
        return WithForm(id, [](RE::TESForm& form) {
            auto* reference = form.As<RE::TESObjectREFR>();
            if (reference == nullptr) {
                throw std::invalid_argument("The supplied form is not a world reference.");
            }
            const auto position = reference->GetPosition();
            const auto* base = reference->GetObjectReference();
            const auto* cell = reference->GetParentCell();
            const auto* location = reference->GetCurrentLocation();
            return DataValue{{"baseFormId", base != nullptr ? base->GetFormID() : 0},
                {"cellFormId", cell != nullptr ? cell->GetFormID() : 0},
                {"locationFormId", location != nullptr ? location->GetFormID() : 0},
                {"position", {{"x", position.x}, {"y", position.y}, {"z", position.z}}}};
        });
    };
    backend.actorValue = [](std::uint32_t id, std::uint32_t valueId, ValueKind kind) {
        return WithForm(id, [&](RE::TESForm& form) {
            auto& actor = Actor(form);
            const auto& value = ActorValue(valueId);
            if (kind == ValueKind::Base) {
                return actor.GetBaseActorValue(value);
            }
            if (kind == ValueKind::Permanent) {
                return actor.GetPermanentActorValue(value);
            }
            return actor.GetActorValue(value);
        });
    };
    backend.setBaseValue = [](std::uint32_t id, std::uint32_t valueId, float amount) {
        WithForm(id, [&](RE::TESForm& form) {
            Actor(form).SetBaseActorValue(ActorValue(valueId), amount);
            return true;
        });
    };
    return backend;
}

const game_events::AdapterRegistration adapter({"forms.session",
    [](game_events::Runtime& runtime, const game_events::Message& message) {
        if (message.Is(game_events::MessageType::PreLoadGame)) {
            World().Ready(false);
            World().Reset();
        } else if (message.Is(game_events::MessageType::NewGame)) {
            World().Reset();
            World().Ready(runtime.DataReady());
        } else if (message.Is(game_events::MessageType::GameDataReady) || message.Is(game_events::MessageType::PostLoadGame)) {
            World().Ready(runtime.DataReady());
        }
    },
    [] { World().Ready(false); }, -100});

const ComponentRegistration component({"forms",
    [](Channel& channel, const std::filesystem::path&) {
        ActiveService() = std::make_unique<Service>(channel, World(), Engine());
    },
    [] {
        World().Ready(false);
        ActiveService().reset();
    }});

}
}
