#include "method_registry.h"
#include "events.h"

#include <limits>

namespace f4forge {
namespace {

F4SEValue TestAdd(std::span<const F4SEValue> arguments) noexcept
{
    if (arguments.size() != 2 || arguments[0].type != ValueType::Int32 || arguments[1].type != ValueType::Int32) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    const auto sum = static_cast<std::int64_t>(arguments[0].integer) + arguments[1].integer;
    if (sum < std::numeric_limits<std::int32_t>::min() || sum > std::numeric_limits<std::int32_t>::max()) {
        return {ValueType::Error, {.error = InvokeError::Overflow}};
    }
    return {ValueType::Int32, {.integer = static_cast<std::int32_t>(sum)}};
}

F4SEValue EmitTestMessage(std::span<const F4SEValue> arguments) noexcept
{
    if (!arguments.empty()) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    const F4SEValue message{ValueType::String, {.utf8String = "Привет из F4Forge"}};
    const bool delivered = PublishEvent("Test_Message", nullptr, message);
    return {ValueType::Int32, {.integer = delivered ? 1 : 0}};
}

}

void RegisterHostMethods(MethodRegistry& registry)
{
    registry.Register("Test_Add", TestAdd);
    registry.Register("Test_EmitMessage", EmitTestMessage);
}

}
