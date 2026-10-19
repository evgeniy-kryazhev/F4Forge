#include "values.h"
#include <cmath>

namespace f4forge::papyrus
{
namespace
{

using Type = RE::BSScript::TypeInfo;
using Raw = Type::RawType;
using Variable = RE::BSScript::Variable;
using Object = RE::BSScript::Object;

void Depth(unsigned depth)
{
    if (depth > 32)
    {
        throw std::invalid_argument("Papyrus values exceed the nesting limit.");
    }
}

bool IsA(const RE::BSScript::ObjectTypeInfo* actual, const RE::BSScript::ObjectTypeInfo* expected)
{
    for (auto* type = actual; type != nullptr; type = type->parentTypeInfo.get())
    {
        if (type == expected)
        {
            return true;
        }
    }
    return false;
}

} // namespace

void Objects::Open(const std::string& scope)
{
    std::scoped_lock lock(gate_);
    scopes_.try_emplace(scope);
}

void Objects::Release(std::string_view scope)
{
    Entries removed;
    {
        std::scoped_lock lock(gate_);
        const auto found = scopes_.find(scope);
        if (found != scopes_.end())
        {
            removed.swap(found->second);
            scopes_.erase(found);
        }
    }
    // Release engine references outside the registry lock.
}

void Objects::Reset()
{
    decltype(scopes_) removed;
    {
        std::scoped_lock lock(gate_);
        removed.swap(scopes_);
        ++session_;
    }
}

std::string Objects::Store(const std::string& scope, RE::BSTSmartPointer<Object> object)
{
    std::scoped_lock lock(gate_);
    const auto found = scopes_.find(scope);
    if (found == scopes_.end())
    {
        throw std::runtime_error("Papyrus scope has closed.");
    }
    for (const auto& [id, existing] : found->second)
    {
        if (existing.get() == object.get())
        {
            return id;
        }
    }
    if (found->second.size() >= 4096)
    {
        throw std::runtime_error("Papyrus scope reached its script identity limit.");
    }
    const auto id = std::to_string(++next_);
    found->second.emplace(id, std::move(object));
    return id;
}

RE::BSTSmartPointer<Object> Objects::Get(const std::string& scope, const std::string& token)
{
    std::scoped_lock lock(gate_);
    const auto found = scopes_.find(scope);
    if (found == scopes_.end() || !found->second.contains(token))
    {
        throw std::invalid_argument("Script identity is stale or belongs to another mod scope.");
    }
    return found->second.at(token);
}

Values::Values(RE::BSScript::IVirtualMachine& vm, std::shared_ptr<Objects> objects, std::string scope,
               std::uint64_t session)
    : vm_(vm), objects_(std::move(objects)), scope_(std::move(scope)), session_(session)
{
}

void Objects::ValidateForm(const DataValue& reference)
{
    if (reference.at("session").get<std::uint64_t>() == 0)
    {
        throw std::invalid_argument("A resolved form session is required.");
    }
    const DataValue request{{"method", "forms.describe"},
                            {"arguments", {{"formId", reference.at("formId")}, {"session", reference.at("session")}}}};
    const auto response = channel_.Serializer().Deserialize(channel_.Call(channel_.Serializer().Serialize(request)));
    if (!response.at("success").get<bool>())
    {
        throw std::invalid_argument(response.at("error").at("message").get<std::string>());
    }
    if (response.at("value").is_null())
    {
        throw std::invalid_argument("Script target form no longer exists.");
    }
}

RE::BSTSmartPointer<Object> Values::Resolve(const DataValue& reference, const Type* expected)
{
    RE::BSTSmartPointer<Object> object;
    if (reference.contains("token") && !reference.at("token").is_null())
    {
        if (reference.at("session").get<std::uint64_t>() != session_)
        {
            throw std::invalid_argument("Script identity belongs to another game session.");
        }
        object = objects_->Get(scope_, reference.at("token").get<std::string>());
    }
    else
    {
        objects_->ValidateForm(reference);
        auto* form = RE::TESForm::GetFormByID(reference.at("formId").get<std::uint32_t>());
        if (form == nullptr)
        {
            throw std::invalid_argument("Script target form no longer exists.");
        }
        const RE::NiPointer<RE::TESObjectREFR> referenceOwner(form->As<RE::TESObjectREFR>());
        const auto handle =
            vm_.GetObjectHandlePolicy().GetHandleForObject(static_cast<std::uint32_t>(form->GetFormType()), form);
        if (handle == vm_.GetObjectHandlePolicy().EmptyHandle())
        {
            throw std::invalid_argument("The form has no Papyrus object handle.");
        }
        auto script = reference.at("scriptName").get<std::string>();
        RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo> nativeType;
        vm_.GetScriptObjectType(static_cast<std::uint32_t>(form->GetFormType()), nativeType);
        RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo> type;
        if (script.empty())
        {
            if (expected != nullptr && expected->IsObject())
            {
                type = RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo>(expected->GetObjectTypeInfo());
            }
            else
            {
                type = nativeType;
            }
            if (!type)
            {
                throw std::invalid_argument("Cannot identify the form's Papyrus type.");
            }
            script = type->name.c_str();
        }
        else
        {
            vm_.GetScriptObjectType(RE::BSFixedString(script), type);
        }
        if (!vm_.FindBoundObject(handle, script.c_str(), false, object, false))
        {
            // Native form views may be materialized by the VM, but never attach
            // an arbitrary user script merely because a caller named it.
            const bool nativeView = nativeType && type && IsA(nativeType.get(), type.get());
            if (!nativeView || !vm_.CreateObject(RE::BSFixedString(script), object) || !object)
            {
                throw std::invalid_argument("The requested script is not attached to the form.");
            }
            vm_.GetObjectBindPolicy().BindObject(object, handle);
        }
    }
    if (!object || !object->IsValid())
    {
        throw std::invalid_argument("The script object is no longer valid.");
    }
    if (expected != nullptr && expected->IsObject() && !IsA(object->type.get(), expected->GetObjectTypeInfo()))
    {
        throw std::invalid_argument("Script argument has an incompatible type.");
    }
    return object;
}

Type Values::Infer(const DataValue& value)
{
    const auto kind = value.at("kind").get<std::string>();
    if (kind == "none")
    {
        return Type(Raw::kNone);
    }
    if (kind == "bool")
    {
        return Type(Raw::kBool);
    }
    if (kind == "int")
    {
        return Type(Raw::kInt);
    }
    if (kind == "float")
    {
        return Type(Raw::kFloat);
    }
    if (kind == "string")
    {
        return Type(Raw::kString);
    }
    if (kind == "object")
    {
        return Type(Resolve(value.at("reference"))->type.get());
    }
    if (kind == "struct")
    {
        RE::BSTSmartPointer<RE::BSScript::StructTypeInfo> type;
        if (!vm_.GetScriptStructType(RE::BSFixedString(value.at("typeName").get<std::string>()), type) || !type)
        {
            throw std::invalid_argument("Unknown Papyrus struct type.");
        }
        return Type(type.get());
    }
    if (kind == "array")
    {
        Type element(Raw::kVar);
        if (value.contains("typeName") && !value.at("typeName").is_null())
        {
            const auto name = value.at("typeName").get<std::string>();
            if (name == "int")
            {
                element = Raw::kInt;
            }
            else if (name == "float")
            {
                element = Raw::kFloat;
            }
            else if (name == "bool")
            {
                element = Raw::kBool;
            }
            else if (name == "string")
            {
                element = Raw::kString;
            }
            else if (name != "var")
            {
                RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo> objectType;
                RE::BSTSmartPointer<RE::BSScript::StructTypeInfo> structType;
                if (vm_.GetScriptObjectType(RE::BSFixedString(name), objectType) && objectType)
                {
                    element = objectType.get();
                }
                else if (vm_.GetScriptStructType(RE::BSFixedString(name), structType) && structType)
                {
                    element = structType.get();
                }
                else
                {
                    throw std::invalid_argument("Unknown Papyrus array element type.");
                }
            }
        }
        element.SetArray(true);
        return element;
    }
    throw std::invalid_argument("Unknown Papyrus value kind.");
}

Variable Values::Decode(const DataValue& value, const Type& expected, unsigned depth)
{
    Depth(depth);
    const auto kind = value.at("kind").get<std::string>();
    const auto raw = expected.GetRawType();
    Variable result;
    if (raw == Raw::kVar)
    {
        auto content = std::make_unique<Variable>(Decode(value, Infer(value), depth + 1));
        // Papyrus Variable owns and deletes its nested Var using the engine heap.
        result = content.release();
        return result;
    }
    if (kind == "none")
    {
        if (expected.IsObject() || expected.IsStruct() || expected.IsArray() || raw == Raw::kNone)
        {
            return result;
        }
        throw std::invalid_argument("None cannot be passed to a primitive Papyrus parameter.");
    }
    if (raw == Raw::kBool && kind == "bool")
    {
        result = value.at("boolean").get<bool>();
    }
    else if (raw == Raw::kInt && kind == "int")
    {
        const auto& wireInteger = value.at("integer");
        if (!wireInteger.is_number_integer() ||
            (wireInteger.is_number_unsigned() && wireInteger.get<std::uint64_t>() > INT32_MAX))
        {
            throw std::invalid_argument("Papyrus integer is out of range or not integral.");
        }
        const auto integer = wireInteger.get<std::int64_t>();
        if (integer < INT32_MIN || integer > INT32_MAX)
        {
            throw std::invalid_argument("Papyrus integer is out of range.");
        }
        result = static_cast<std::int32_t>(integer);
    }
    else if (raw == Raw::kFloat && (kind == "float" || kind == "int"))
    {
        const auto number = kind == "int" ? static_cast<float>(value.at("integer").get<std::int32_t>())
                                          : value.at("number").get<float>();
        if (!std::isfinite(number))
        {
            throw std::invalid_argument("Papyrus floats must be finite.");
        }
        result = number;
    }
    else if (raw == Raw::kString && kind == "string")
    {
        const auto text = value.at("text").get<std::string>();
        if (text.find('\0') != std::string::npos)
        {
            throw std::invalid_argument("Papyrus strings cannot contain NUL.");
        }
        result = RE::BSFixedString(text);
    }
    else if (expected.IsObject() && kind == "object")
    {
        result = Resolve(value.at("reference"), &expected);
    }
    else if (expected.IsArray() && kind == "array")
    {
        const auto& items = value.at("items");
        if (!items.is_array() || items.size() > 65536)
        {
            throw std::invalid_argument("Invalid Papyrus array size.");
        }
        Type element;
        if (expected.IsComplex())
        {
            element = expected;
            element.SetArray(false);
        }
        else
        {
            element = static_cast<Raw>(static_cast<unsigned>(raw) - static_cast<unsigned>(Raw::kArrayStart));
        }
        RE::BSTSmartPointer<RE::BSScript::Array> array;
        if (!vm_.CreateArray(element, static_cast<std::uint32_t>(items.size()), array) || !array)
        {
            throw std::runtime_error("Cannot allocate a Papyrus array.");
        }
        for (std::uint32_t index = 0; index < array->size(); ++index)
        {
            (*array)[index] = Decode(items.at(index), element, depth + 1);
        }
        result = std::move(array);
    }
    else if (expected.IsStruct() && kind == "struct")
    {
        auto* type = expected.GetStructTypeInfo();
        const auto name = value.at("typeName").get<std::string>();
        if (name != type->GetName())
        {
            throw std::invalid_argument("Papyrus struct type does not match the parameter.");
        }
        RE::BSTSmartPointer<RE::BSScript::Struct> structure;
        if (!vm_.CreateStruct(RE::BSFixedString(name), structure) || !structure)
        {
            throw std::runtime_error("Cannot allocate a Papyrus struct.");
        }
        for (const auto& [field, fieldValue] : value.at("fields").items())
        {
            const auto found = structure->type->varNameIndexMap.find(RE::BSFixedString(field));
            if (found == structure->type->varNameIndexMap.end())
            {
                throw std::invalid_argument("Unknown Papyrus struct field: " + field);
            }
            const auto index = found->second;
            structure->variables[index] = Decode(fieldValue, structure->type->variables[index].varType, depth + 1);
        }
        result = std::move(structure);
    }
    else
    {
        throw std::invalid_argument("Papyrus argument does not match VM metadata.");
    }
    return result;
}

DataValue Values::Encode(const Variable& value, unsigned depth)
{
    Depth(depth);
    using RE::BSScript::get;
    if (value.is<std::nullptr_t>())
    {
        return {{"kind", "none"}};
    }
    if (value.is<bool>())
    {
        return {{"kind", "bool"}, {"boolean", get<bool>(value)}};
    }
    if (value.is<std::int32_t>())
    {
        return {{"kind", "int"}, {"integer", get<std::int32_t>(value)}};
    }
    if (value.is<float>())
    {
        const auto number = get<float>(value);
        if (!std::isfinite(number))
        {
            throw std::runtime_error("Papyrus returned a non-finite float.");
        }
        return {{"kind", "float"}, {"number", number}};
    }
    if (value.is<RE::BSFixedString>())
    {
        return {{"kind", "string"}, {"text", get<RE::BSFixedString>(value).c_str()}};
    }
    if (value.is<Variable>())
    {
        const auto* nested = get<Variable>(value);
        return nested != nullptr ? Encode(*nested, depth + 1) : DataValue{{"kind", "none"}};
    }
    if (value.is<Object>())
    {
        const auto object = get<Object>(value);
        if (!object)
        {
            return {{"kind", "none"}};
        }
        if (!object->IsValid() || !object->type)
        {
            throw std::runtime_error("Papyrus returned an invalid script object.");
        }
        return {{"kind", "object"},
                {"reference",
                 {{"formId", 0},
                  {"session", session_},
                  {"scriptName", object->type->name.c_str()},
                  {"token", objects_->Store(scope_, object)}}}};
    }
    if (value.is<RE::BSScript::Array>())
    {
        const auto array = get<RE::BSScript::Array>(value);
        if (!array)
        {
            return {{"kind", "none"}};
        }
        std::vector<Variable> snapshot;
        {
            RE::BSAutoLock lock(array->elementsLock);
            if (array->size() > 65536)
            {
                throw std::runtime_error("Papyrus array exceeds the element limit.");
            }
            snapshot.assign(array->begin(), array->end());
        }
        // Copy first: recursive values can refer back to this array. Never hold
        // a VM collection lock while walking other values or storing identities.
        DataValue items = DataValue::array();
        for (const auto& item : snapshot)
        {
            items.push_back(Encode(item, depth + 1));
        }
        std::string elementName;
        const auto type = array->elementType;
        if (type.IsObject())
        {
            elementName = type.GetObjectTypeInfo()->name.c_str();
        }
        else if (type.IsStruct())
        {
            elementName = type.GetStructTypeInfo()->GetName();
        }
        else
        {
            switch (type.GetRawType())
            {
            case Raw::kInt:
                elementName = "int";
                break;
            case Raw::kFloat:
                elementName = "float";
                break;
            case Raw::kBool:
                elementName = "bool";
                break;
            case Raw::kString:
                elementName = "string";
                break;
            case Raw::kVar:
                elementName = "var";
                break;
            default:
                throw std::runtime_error("Unsupported Papyrus array element type.");
            }
        }
        return {{"kind", "array"}, {"typeName", elementName}, {"items", std::move(items)}};
    }
    if (value.is<RE::BSScript::Struct>())
    {
        const auto structure = get<RE::BSScript::Struct>(value);
        if (!structure)
        {
            return {{"kind", "none"}};
        }
        std::vector<std::pair<std::string, Variable>> snapshot;
        {
            RE::BSAutoLock lock(structure->structLock);
            for (const auto& [name, index] : structure->type->varNameIndexMap)
            {
                snapshot.emplace_back(name.c_str(), structure->variables[index]);
            }
        }
        DataValue fields = DataValue::object();
        for (const auto& [name, item] : snapshot)
        {
            fields[name] = Encode(item, depth + 1);
        }
        return {{"kind", "struct"}, {"typeName", structure->type->GetName()}, {"fields", std::move(fields)}};
    }
    throw std::runtime_error("Unsupported Papyrus result type.");
}

} // namespace f4forge::papyrus
