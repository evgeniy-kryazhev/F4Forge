#include "../extensions/transport.h"
#include "components.h"
#include "integer.h"
#include <cmath>

namespace f4forge::papyrus_registration
{
namespace
{
using Variable = RE::BSScript::Variable;
using Raw = RE::BSScript::TypeInfo::RawType;

Raw Type(const std::string& name)
{
    if (name == "none")
    {
        return Raw::kNone;
    }
    if (name == "bool")
    {
        return Raw::kBool;
    }
    if (name == "int")
    {
        return Raw::kInt;
    }
    if (name == "float")
    {
        return Raw::kFloat;
    }
    if (name == "string")
    {
        return Raw::kString;
    }
    throw std::invalid_argument("Unsupported native function type: " + name);
}

DataValue Read(const Variable& value)
{
    if (value.is<bool>())
    {
        return RE::BSScript::get<bool>(value);
    }
    if (value.is<std::int32_t>())
    {
        return RE::BSScript::get<std::int32_t>(value);
    }
    if (value.is<float>())
    {
        return RE::BSScript::get<float>(value);
    }
    if (value.is<RE::BSFixedString>())
    {
        return std::string(RE::BSScript::get<RE::BSFixedString>(value).c_str());
    }
    if (value.is<std::nullptr_t>())
    {
        return nullptr;
    }
    throw std::invalid_argument("Expected a primitive Papyrus value.");
}

void Write(Variable& output, Raw type, const DataValue& value)
{
    switch (type)
    {
    case Raw::kNone:
        if (!value.is_null())
        {
            throw std::invalid_argument("A void function must return null.");
        }
        output = nullptr;
        break;
    case Raw::kBool:
        output = value.get<bool>();
        break;
    case Raw::kInt:
        output = ReadInteger(value);
        break;
    case Raw::kFloat: {
        const auto number = value.get<float>();
        if (!std::isfinite(number))
        {
            throw std::invalid_argument("Expected a finite float.");
        }
        output = number;
        break;
    }
    case Raw::kString:
        output = RE::BSFixedString(value.get<std::string>());
        break;
    default:
        throw std::invalid_argument("Unsupported Papyrus return type.");
    }
}

struct Binding
{
    Binding(std::string callbackName, Channel* owner) : callback(std::move(callbackName)), channel(owner)
    {
    }

    std::string callback;
    std::atomic<Channel*> channel;
    std::atomic<bool> active = true;
};

// The VM owns native functions permanently. Only their revocable binding refers
// to a mod; closing a registration leaves a safe, inert VM stub.
class Function final : public RE::BSScript::NF_util::NativeFunctionBase
{
  public:
    Function(const DataValue& request, std::shared_ptr<Binding> binding)
        : NativeFunctionBase(request.at("script").get<std::string>(), request.at("name").get<std::string>(),
                             static_cast<std::uint16_t>(request.at("parameters").size()), true, false),
          binding_(std::move(binding))
    {
        retType = Type(request.at("returnType").get<std::string>());
        for (std::uint16_t index = 0; index < descTable.paramCount; ++index)
        {
            const auto type = Type(request.at("parameters").at(index).get<std::string>());
            if (type == Raw::kNone)
            {
                throw std::invalid_argument("Parameters cannot have type none.");
            }
            descTable.entries[index].second = type;
        }
    }

    bool HasStub() const override
    {
        return true;
    }

    bool MarshallAndDispatch(Variable&, RE::BSScript::Internal::VirtualMachine&, std::uint32_t, Variable& result,
                             const RE::BSScript::StackFrame& frame) const override
    {
        result = nullptr;
        bool success = false;
        game_events::Host().Invoke([&] {
            auto* owner = binding_->channel.load();
            if (owner == nullptr)
            {
                return;
            }
            owner->Dispatch([&](const DataValue&) -> DataValue {
                if (!binding_->active)
                {
                    return nullptr;
                }
                if (frame.parent == nullptr)
                {
                    throw std::runtime_error("Papyrus call has no stack.");
                }
                auto arguments = DataValue::array();
                const auto page = frame.GetPageForFrame();
                for (std::uint32_t index = 0; index < descTable.paramCount; ++index)
                {
                    arguments.push_back(Read(frame.GetStackFrameVariable(index, page)));
                }
                const auto value = extensions::Call(*owner, binding_->callback, arguments);
                Write(result, retType.GetRawType(), value);
                success = true;
                return nullptr;
            });
        });
        return success;
    }

  private:
    std::shared_ptr<Binding> binding_;
};

Channel* channel = nullptr;
std::vector<std::uint64_t> routes;
std::map<std::string, std::shared_ptr<Binding>> bindings;

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    routes.push_back(owner.Add("papyrus.functions.register", [&owner](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        const auto script = request.at("script").get<std::string>();
        const auto name = request.at("name").get<std::string>();
        if (script.empty() || name.empty() || script.size() > 256 || name.size() > 256 ||
            request.at("parameters").size() > 16)
        {
            throw std::invalid_argument("Invalid Papyrus function signature.");
        }
        const auto key = script + ":" + name;
        if (bindings.contains(key))
        {
            throw std::invalid_argument("Papyrus function was already registered: " + key);
        }
        auto* game = RE::GameVM::GetSingleton();
        const auto vm = game != nullptr ? game->GetVM() : nullptr;
        if (vm == nullptr)
        {
            throw std::runtime_error("Papyrus VM is unavailable.");
        }
        auto binding = std::make_shared<Binding>(request.at("callback").get<std::string>(), &owner);
        auto function = std::make_unique<Function>(request, binding);
        if (!vm->BindNativeMethod(function.get()))
        {
            throw std::runtime_error("VM refused the native function.");
        }
        // BindNativeMethod transfers ownership to the VM, which uses its own heap.
        function.release();
        bindings.emplace(key, std::move(binding));
        return key;
    }));
    routes.push_back(owner.Add("papyrus.functions.release", [](const DataValue& request) -> DataValue {
        const auto found = bindings.find(request.at("key").get<std::string>());
        if (found != bindings.end())
        {
            found->second->active = false;
        }
        return true;
    }));
    routes.push_back(owner.Add("papyrus.external.send", [](const DataValue& request) -> DataValue {
        extensions::RequireGameThread();
        const auto* papyrus = F4SE::GetPapyrusInterface();
        if (papyrus == nullptr)
        {
            throw std::runtime_error("F4SE Papyrus interface is unavailable.");
        }
        auto arguments = request.at("arguments");
        const auto visitor = [](std::uint64_t handle, const char* script, const char* callback, void* data) {
            game_events::Host().Invoke([&] {
                const auto values = *static_cast<const DataValue*>(data);
                auto* game = RE::GameVM::GetSingleton();
                const auto vm = game != nullptr ? game->GetVM() : nullptr;
                if (vm == nullptr)
                {
                    return;
                }
                RE::BSTThreadScrapFunction<bool(RE::BSScrapArray<Variable>&)> factory =
                    [values](RE::BSScrapArray<Variable>& packed) {
                        try
                        {
                            for (const auto& value : values)
                            {
                                Variable item;
                                const auto type = value.is_boolean()          ? Raw::kBool
                                                  : value.is_number_integer() ? Raw::kInt
                                                  : value.is_number()         ? Raw::kFloat
                                                  : value.is_string()         ? Raw::kString
                                                                              : Raw::kNone;
                                Write(item, type, value);
                                packed.push_back(item);
                            }
                            return true;
                        }
                        catch (...)
                        {
                            return false;
                        }
                    };
                if (!vm->DispatchMethodCall(handle, RE::BSFixedString(script), RE::BSFixedString(callback), factory,
                                            nullptr))
                {
                    game_events::Host().Report("VM rejected an external event callback.");
                }
            });
        };
        papyrus->GetExternalEventRegistrations(request.at("name").get<std::string>(), &arguments, visitor);
        return true;
    }));
}

void Stop()
{
    if (channel == nullptr)
    {
        return;
    }
    channel->Dispatch([](const DataValue&) -> DataValue {
        for (const auto& [key, binding] : bindings)
        {
            binding->active = false;
            binding->channel = nullptr;
        }
        return nullptr;
    });
    for (const auto route : routes)
    {
        channel->Remove(route);
    }
    routes.clear();
    channel = nullptr;
}

const ComponentRegistration component({"papyrus.registration", Start, Stop});
} // namespace
} // namespace f4forge::papyrus_registration
