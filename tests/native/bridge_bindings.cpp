#include "method_registry.h"

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

}

void RegisterHostMethods(MethodRegistry& registry)
{
    registry.Register("Test_Add", TestAdd);
}

}
