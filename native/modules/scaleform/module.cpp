#include "../extensions/transport.h"
#include "components.h"
#include <Scaleform/G/GFx_ASMovieRootBase.h>

namespace f4forge::scaleform
{
namespace
{
using Value = Scaleform::GFx::Value;
Channel* channel = nullptr;
std::vector<std::uint64_t> routes;

Scaleform::Ptr<Scaleform::GFx::Movie> Movie(const DataValue& request)
{
    extensions::RequireGameThread();
    const auto* ui = RE::UI::GetSingleton();
    const auto menu = ui != nullptr ? ui->GetMenu(RE::BSFixedString(request.at("menu").get<std::string>())) : nullptr;
    if (!menu || !menu->OnStack() || !menu->uiMovie)
    {
        throw std::runtime_error("The requested Scaleform menu is not open.");
    }
    return menu->uiMovie;
}

DataValue Read(const Value& value, unsigned depth = 0)
{
    if (depth > 32)
    {
        throw std::invalid_argument("Scaleform value exceeds the nesting limit.");
    }
    if (value.IsUndefined() || value.GetType() == Value::ValueType::kNull)
    {
        return nullptr;
    }
    if (value.IsBoolean())
    {
        return value.GetBoolean();
    }
    if (value.IsInt())
    {
        return value.GetInt();
    }
    if (value.IsUInt())
    {
        return value.GetUInt();
    }
    if (value.IsNumber())
    {
        return value.GetNumber();
    }
    if (value.IsString())
    {
        return std::string(value.GetString());
    }
    if (value.IsArray())
    {
        if (value.GetArraySize() > 65536)
        {
            throw std::invalid_argument("Scaleform array is too large.");
        }
        auto result = DataValue::array();
        for (std::uint32_t index = 0; index < value.GetArraySize(); ++index)
        {
            Value item;
            if (!value.GetElement(index, &item))
            {
                throw std::runtime_error("Cannot read Scaleform array element.");
            }
            result.push_back(Read(item, depth + 1));
        }
        return result;
    }
    throw std::invalid_argument("Only copied primitive values and arrays can leave a Scaleform movie.");
}

Value Write(Scaleform::GFx::Movie& movie, const DataValue& value, unsigned depth = 0)
{
    if (depth > 32)
    {
        throw std::invalid_argument("Scaleform value exceeds the nesting limit.");
    }
    if (value.is_null())
    {
        return Value(nullptr);
    }
    if (value.is_boolean())
    {
        return Value(value.get<bool>());
    }
    if (value.is_number())
    {
        if (value.is_number_integer() && value.get<std::int64_t>() >= INT32_MIN &&
            value.get<std::int64_t>() <= INT32_MAX)
        {
            return Value(value.get<std::int32_t>());
        }
        return Value(value.get<double>());
    }
    if (value.is_string())
    {
        Value result;
        const auto text = value.get<std::string>();
        // A movie-owned string must survive destruction of this temporary text.
        movie.asMovieRoot->CreateString(&result, text.c_str());
        return result;
    }
    if (value.is_array())
    {
        Value result;
        movie.CreateArray(&result);
        for (const auto& item : value)
        {
            result.PushBack(Write(movie, item, depth + 1));
        }
        return result;
    }
    throw std::invalid_argument("Expected a primitive Scaleform value or array.");
}

void Start(Channel& owner, const std::filesystem::path&)
{
    channel = &owner;
    routes.push_back(owner.Add("ui.movie.read", [](const DataValue& request) -> DataValue {
        const auto movie = Movie(request);
        Value result;
        if (!movie->GetVariable(&result, request.at("path").get<std::string>().c_str()))
        {
            throw std::invalid_argument("Scaleform variable was not found.");
        }
        return Read(result);
    }));
    routes.push_back(owner.Add("ui.movie.write", [](const DataValue& request) -> DataValue {
        const auto movie = Movie(request);
        const auto value = Write(*movie, request.at("value"));
        if (!movie->SetVariable(request.at("path").get<std::string>().c_str(), value))
        {
            throw std::runtime_error("Scaleform variable write failed.");
        }
        return true;
    }));
    routes.push_back(owner.Add("ui.movie.invoke", [](const DataValue& request) -> DataValue {
        const auto movie = Movie(request);
        std::vector<Value> arguments;
        for (const auto& argument : request.at("arguments"))
        {
            arguments.push_back(Write(*movie, argument));
        }
        Value result;
        if (!movie->Invoke(request.at("path").get<std::string>().c_str(), &result, arguments.data(),
                           static_cast<std::uint32_t>(arguments.size())))
        {
            throw std::runtime_error("Scaleform function invocation failed.");
        }
        return request.value("discardResult", false) ? DataValue(true) : Read(result);
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

const ComponentRegistration component({"ui.scaleform", Start, Stop});
} // namespace
} // namespace f4forge::scaleform
