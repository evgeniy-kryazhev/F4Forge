#include "../game_events/runtime.h"
#include "components.h"
#include "service.h"
#include "values.h"
#include <F4SE/F4SE.h>

namespace f4forge::papyrus
{
namespace
{

using VM = RE::BSScript::IVirtualMachine;
using Variable = RE::BSScript::Variable;
using Function = RE::BSScript::IFunction;
using ObjectType = RE::BSScript::ObjectTypeInfo;

std::atomic<bool> loading = false;

void RequireSession(const std::shared_ptr<Objects>& objects, std::uint64_t expected)
{
    const auto* main = RE::Main::GetSingleton();
    if (main == nullptr || main->threadID == 0 || main->threadID != REX::W32::GetCurrentThreadId())
    {
        throw std::runtime_error("Papyrus submission and conversion require the game thread.");
    }
    if (loading || !game_events::Host().DataReady() || objects->Session() != expected)
    {
        throw std::runtime_error("Papyrus game session is unavailable or has changed.");
    }
}

bool SameName(const RE::BSFixedString& name, const std::string& requested)
{
    return _stricmp(name.c_str(), requested.c_str()) == 0;
}

RE::BSTSmartPointer<Function> FindFunction(ObjectType* type, const std::string& name, const RE::BSFixedString& state,
                                           bool global)
{
    for (; type != nullptr; type = type->parentTypeInfo.get())
    {
        if (global)
        {
            for (std::uint32_t index = 0; index < type->GetNumGlobalFuncs(); ++index)
            {
                const auto function = type->GetGlobalFuncIter()[index].func;
                if (function && SameName(function->GetName(), name))
                {
                    return function;
                }
            }
        }
        else
        {
            for (std::uint32_t index = 0; index < type->GetNumNamedStates(); ++index)
            {
                auto& named = type->GetNamedStateIter()[index];
                if (named.name != state)
                {
                    continue;
                }
                for (std::uint32_t functionIndex = 0; functionIndex < named.GetNumFuncs(); ++functionIndex)
                {
                    const auto function = named.GetFuncIter()[functionIndex].func;
                    if (function && SameName(function->GetName(), name))
                    {
                        return function;
                    }
                }
            }
            for (std::uint32_t index = 0; index < type->GetNumMemberFuncs(); ++index)
            {
                const auto function = type->GetMemberFuncIter()[index].func;
                if (function && SameName(function->GetName(), name))
                {
                    return function;
                }
            }
        }
    }
    throw std::invalid_argument("Papyrus function was not found: " + name);
}

const RE::BSScript::PropertyTypeInfo& FindProperty(ObjectType* type, const std::string& name)
{
    for (; type != nullptr; type = type->parentTypeInfo.get())
    {
        for (std::uint32_t index = 0; index < type->GetNumProperties(); ++index)
        {
            const auto& property = type->GetPropertyIter()[index];
            if (SameName(property.name, name))
            {
                return property.info;
            }
        }
    }
    throw std::invalid_argument("Papyrus property was not found: " + name);
}

void Queue(std::function<void()> task)
{
    const auto* tasks = F4SE::GetTaskInterface();
    if (tasks == nullptr)
    {
        throw std::runtime_error("F4SE task interface is unavailable.");
    }
    tasks->AddTask(std::move(task));
}

class Callback final : public RE::BSScript::IStackCallbackFunctor
{
  public:
    Callback(std::shared_ptr<Values> values, std::shared_ptr<Objects> objects, std::uint64_t session,
             Completion complete, IsPending pending)
        : values_(std::move(values)), objects_(std::move(objects)), session_(session), complete_(std::move(complete)),
          pending_(std::move(pending))
    {
    }

    void CallQueued() override
    {
    }
    void StartMultiDispatch() override
    {
    }
    void EndMultiDispatch() override
    {
    }

    void CallCanceled() override
    {
        CompleteFailure("Papyrus VM canceled the operation.");
    }

    void operator()(Variable result) override
    {
        try
        {
            if (!pending_())
            {
                return;
            }
            // VM callbacks may run on tasklet threads. Retain the ref-counted
            // value and copy its data on the game thread before SDK publication.
            Queue([values = values_, objects = objects_, session = session_, complete = complete_, pending = pending_,
                   result]() {
                game_events::Host().Invoke([&] {
                    if (!pending())
                    {
                        return;
                    }
                    try
                    {
                        RequireSession(objects, session);
                        complete(values->Encode(result), "");
                    }
                    catch (const std::exception& exception)
                    {
                        complete(nullptr, exception.what());
                    }
                    catch (...)
                    {
                        complete(nullptr, "Unknown Papyrus result conversion failure.");
                    }
                });
            });
        }
        catch (const std::exception& exception)
        {
            CompleteFailure(exception.what());
        }
        catch (...)
        {
            CompleteFailure("Unknown Papyrus callback failure.");
        }
    }

  private:
    void CompleteFailure(std::string error) noexcept
    {
        try
        {
            complete_(nullptr, std::move(error));
        }
        catch (...)
        {
            game_events::Host().Report("Cannot report a Papyrus callback failure.");
        }
    }

    std::shared_ptr<Values> values_;
    std::shared_ptr<Objects> objects_;
    std::uint64_t session_;
    Completion complete_;
    IsPending pending_;
};

void Execute(const DataValue& request, const std::shared_ptr<Objects>& objects, Completion complete, IsPending pending)
{
    if (!pending())
    {
        return;
    }
    try
    {
        const auto session = objects->Session();
        RequireSession(objects, session);
        const auto game = RE::GameVM::GetSingleton();
        const auto vm = game != nullptr ? game->GetVM() : nullptr;
        if (vm == nullptr)
        {
            throw std::runtime_error("Papyrus VM is unavailable.");
        }
        auto values = std::make_shared<Values>(*vm, objects, request.at("scope").get<std::string>(), session);
        const auto script = request.at("scriptName").get<std::string>();
        const auto name = request.at("name").get<std::string>();
        const auto operation = request.at("operation").get<std::string>();
        RE::BSTSmartPointer<RE::BSScript::Object> target;
        RE::BSTSmartPointer<ObjectType> type;
        if (operation == "static")
        {
            if (!vm->GetScriptObjectType(RE::BSFixedString(script), type) || !type || !type->Valid())
            {
                throw std::invalid_argument("Unknown or invalid Papyrus script: " + script);
            }
        }
        else
        {
            target = values->Resolve(request.at("target"));
            type = target->type;
        }
        RE::BSTSmartPointer<RE::BSScript::IStackCallbackFunctor> callback =
            RE::make_smart<Callback>(values, objects, session, complete, pending);
        bool accepted = false;
        if (operation == "get" || operation == "set")
        {
            const auto& property = FindProperty(type.get(), name);
            if (operation == "get")
            {
                accepted = vm->GetPropertyValue(target, name.c_str(), callback);
            }
            else
            {
                if (request.at("arguments").size() != 1)
                {
                    throw std::invalid_argument("A property write requires exactly one argument.");
                }
                const auto value = values->Decode(request.at("arguments").at(0), property.type);
                accepted = vm->SetPropertyValue(target, name.c_str(), value, callback);
            }
        }
        else
        {
            const auto function = FindFunction(type.get(), name, target ? target->currentState : RE::BSFixedString(),
                                               operation == "static");
            const auto& arguments = request.at("arguments");
            if (arguments.size() > function->GetParamCount())
            {
                throw std::invalid_argument("Too many arguments for the Papyrus function.");
            }
            auto converted = std::make_shared<std::vector<Variable>>();
            converted->reserve(arguments.size());
            for (std::uint32_t index = 0; index < arguments.size(); ++index)
            {
                RE::BSFixedString parameterName;
                RE::BSScript::TypeInfo parameterType;
                function->GetParam(index, parameterName, parameterType);
                converted->push_back(values->Decode(arguments.at(index), parameterType));
            }
            RE::BSTThreadScrapFunction<bool(RE::BSScrapArray<Variable>&)> factory =
                [converted](RE::BSScrapArray<Variable>& packed) {
                    try
                    {
                        for (const auto& value : *converted)
                        {
                            packed.push_back(value);
                        }
                        return true;
                    }
                    catch (...)
                    {
                        // VM may execute this factory after dispatch has returned.
                        // Exceptions must never escape into its tasklet worker.
                        return false;
                    }
                };
            if (operation == "static")
            {
                accepted =
                    vm->DispatchStaticCall(RE::BSFixedString(script), RE::BSFixedString(name), factory, callback);
            }
            else
            {
                accepted = vm->DispatchMethodCall(target, RE::BSFixedString(name), factory, callback);
            }
        }
        if (!accepted)
        {
            throw std::runtime_error("Papyrus VM rejected the operation; check its log for details.");
        }
    }
    catch (const std::exception& exception)
    {
        complete(nullptr, exception.what());
    }
    catch (...)
    {
        complete(nullptr, "Unknown Papyrus dispatch failure.");
    }
}

std::atomic<std::shared_ptr<Service>> service;

Backend Engine(Channel& channel)
{
    auto objects = std::make_shared<Objects>(channel);
    Backend backend;
    backend.submit = [objects](const DataValue& request, Completion complete, IsPending pending) {
        objects->Open(request.at("scope").get<std::string>());
        Queue([objects, request, complete = std::move(complete), pending = std::move(pending)] {
            game_events::Host().Invoke([&] { Execute(request, objects, complete, pending); });
        });
    };
    backend.release = [objects](std::string_view scope) { objects->Release(scope); };
    backend.reset = [objects] { objects->Reset(); };
    return backend;
}

void OnMessage(game_events::Runtime& runtime, const game_events::Message& message)
{
    if (message.Is(game_events::MessageType::PreLoadGame))
    {
        loading = true;
    }
    else if (message.Is(game_events::MessageType::NewGame) || message.Is(game_events::MessageType::PostLoadGame))
    {
        loading = false;
    }
    const bool dataUnloaded = message.Is(game_events::MessageType::GameDataReady) && !runtime.DataReady();
    const auto activeService = service.load();
    if (activeService && (message.Is(game_events::MessageType::PreLoadGame) ||
                          message.Is(game_events::MessageType::NewGame) || dataUnloaded))
    {
        activeService->Reset();
    }
}

void Start(Channel& channel, const std::filesystem::path&)
{
    loading = false;
    service.store(std::make_shared<Service>(channel, Engine(channel), [&channel](const DataValue& result) {
        game_events::Host().Invoke([&] {
            if (channel.Serializer().Serialize(result).size() > Channel::MaxMessageBytes - 128)
            {
                game_events::Host().Publish("papyrus.completed",
                                            {{"requestId", result.at("requestId")},
                                             {"success", false},
                                             {"value", nullptr},
                                             {"error", "Papyrus result exceeds the transport limit."}});
            }
            else
            {
                game_events::Host().Publish("papyrus.completed", result);
            }
        });
    }));
}

void Stop()
{
    // A concurrent F4SE notification may retain a snapshot. Disable the channel
    // synchronously; its snapshot keeps the service object alive until it exits.
    const auto activeService = service.exchange(nullptr);
    if (activeService)
    {
        activeService->Stop();
    }
}

const game_events::AdapterRegistration adapter({"papyrus.session", OnMessage, [] {}, -90});
const ComponentRegistration component({"papyrus", Start, Stop});

} // namespace
} // namespace f4forge::papyrus
