#pragma once

#include <cstdint>
#include <type_traits>

namespace f4forge::core::forms {

inline constexpr char GetInfoEndpoint[] = "game.forms.get_info";
inline constexpr char GetNameEndpoint[] = "game.forms.get_name";
inline constexpr uint32_t EndpointVersion = 1;
inline constexpr uint32_t NameCapacity = 256;

enum class FormType : uint32_t {
    Unknown = 0,
    Item = 1,
    Actor = 2,
    Reference = 3,
    Cell = 4,
    Worldspace = 5,
    Quest = 6,
    Other = 255
};

struct FormRequest final { uint32_t formId; };
struct FormInfoResponse final {
    uint32_t formId;
    uint32_t formType;
    uint32_t isValid;
};
struct FormNameResponse final {
    uint32_t length;
    char utf8[NameCapacity];
};

static_assert(std::is_trivial_v<FormRequest> && sizeof(FormRequest) == 4);
static_assert(std::is_trivial_v<FormInfoResponse> && sizeof(FormInfoResponse) == 12);
static_assert(std::is_trivial_v<FormNameResponse> && sizeof(FormNameResponse) == 260);

}
