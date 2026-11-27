#pragma once

#include <cstddef>
#include <cstdint>

namespace f4forge {

enum class ValueType : std::int32_t {
    Error = -1,
    Int32 = 2,
    String = 6
};

enum class InvokeError : std::int32_t {
    UnknownOperation = 1,
    InvalidArguments = 2,
    Overflow = 3,
    RegistryUnavailable = 4
};

// The payload occupies one 64-bit slot, including for 32-bit values.
struct F4SEValue {
    ValueType type;
    union {
        std::int32_t integer;
        const char* utf8String;
        InvokeError error;
        std::uint64_t storage;
    };
};

static_assert(offsetof(F4SEValue, integer) == 8);
static_assert(sizeof(F4SEValue) == 16);

using InvokeFunction = void (__cdecl*)(const char* name, const F4SEValue* arguments,
    std::int32_t count, F4SEValue* result) noexcept;

}
