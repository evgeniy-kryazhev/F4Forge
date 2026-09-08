#pragma once

#include "f4forge_abi.h"
#include <stdint.h>

#ifdef __cplusplus
namespace f4forge {

struct EndpointHandle final {
    uint64_t value{};
};

struct EventSubscriptionHandle final {
    uint64_t value{};
};

struct InterceptorSubscriptionHandle final {
    uint64_t value{};
};

struct ModuleHandle final {
    uint64_t value{};
};

struct RuntimeHandle final {
    uint64_t value{};
};

struct PluginHandle final {
    uint64_t value{};
};

constexpr uint32_t HandleIndex(uint64_t value) noexcept
{
    return static_cast<uint32_t>(value);
}

constexpr uint32_t HandleGeneration(uint64_t value) noexcept
{
    return static_cast<uint32_t>(value >> 32);
}

constexpr uint64_t MakeHandle(uint32_t generation, uint32_t index) noexcept
{
    return (static_cast<uint64_t>(generation) << 32) | index;
}

static_assert(sizeof(EndpointHandle) == sizeof(uint64_t));
static_assert(sizeof(ModuleHandle) == sizeof(uint64_t));
static_assert(MakeHandle(7, 11) == UINT64_C(0x000000070000000B));
static_assert(HandleGeneration(MakeHandle(7, 11)) == 7);
static_assert(HandleIndex(MakeHandle(7, 11)) == 11);

}
#endif
