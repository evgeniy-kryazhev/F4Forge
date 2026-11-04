#pragma once

#include "serialization.h"
#include <cstdint>
#include <limits>
#include <stdexcept>

namespace f4forge::papyrus_registration
{
inline std::int32_t ReadInteger(const DataValue& value)
{
    constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
    constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
    // Check unsigned values before converting: UINT64_MAX would otherwise become -1.
    if (value.is_number_unsigned())
    {
        if (value.get<std::uint64_t>() > static_cast<std::uint64_t>(maximum))
        {
            throw std::out_of_range("Papyrus integer is out of range.");
        }
    }
    else if (value.is_number_integer())
    {
        const auto number = value.get<std::int64_t>();
        if (number < minimum || number > maximum)
        {
            throw std::out_of_range("Papyrus integer is out of range.");
        }
    }
    else
    {
        throw std::invalid_argument("Expected a signed integer.");
    }
    return value.get<std::int32_t>();
}
}
