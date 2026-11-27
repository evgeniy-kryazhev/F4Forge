#pragma once

#include "method_registry.h"
#include <ffi.h>
#include <array>
#include <functional>
#include <type_traits>

namespace f4forge::bindings {

inline void Register(MethodRegistry& registry, const char* signature, const char* shortName, MethodHandler handler)
{
    registry.Register(signature, handler);
    registry.Register(shortName, handler);
}

// libffi only sees scalar C arguments. References are borrowed pointers, and C++
// performs member dispatch (including virtual dispatch) inside the compiled thunk.
template<class T> struct WireType { using Type = T; };
template<class T> struct WireType<T*> { using Type = void*; };
template<class T> struct WireType<T&> { using Type = void*; };
template<> struct WireType<bool> { using Type = std::uint8_t; };
template<class T> requires std::is_enum_v<T>
struct WireType<T> { using Type = std::underlying_type_t<T>; };
template<class T> using Wire = typename WireType<T>::Type;

template<class T>
constexpr ValueType Tag()
{
    if constexpr (std::is_enum_v<T>) {
        return Tag<std::underlying_type_t<T>>();
    } else if constexpr (std::is_pointer_v<T> || std::is_reference_v<T>) {
        return ValueType::Pointer;
    } else if constexpr (std::is_same_v<T, bool>) {
        return ValueType::Bool;
    } else if constexpr (std::is_same_v<T, int>) {
        return ValueType::Int32;
    } else if constexpr (std::is_same_v<T, unsigned int>) {
        return ValueType::UInt32;
    } else if constexpr (std::is_same_v<T, float>) {
        return ValueType::Float;
    } else {
        static_assert(std::is_void_v<T>, "Unsupported binding type");
        return ValueType::Void;
    }
}

template<class T>
ffi_type* FfiType()
{
    if constexpr (Tag<T>() == ValueType::Pointer) {
        return &ffi_type_pointer;
    } else if constexpr (Tag<T>() == ValueType::Bool) {
        return &ffi_type_uint8;
    } else if constexpr (Tag<T>() == ValueType::Int32) {
        return &ffi_type_sint32;
    } else if constexpr (Tag<T>() == ValueType::UInt32) {
        return &ffi_type_uint32;
    } else {
        static_assert(Tag<T>() == ValueType::Float);
        return &ffi_type_float;
    }
}

template<class T>
decltype(auto) Decode(Wire<T> value)
{
    if constexpr (std::is_reference_v<T>) {
        return *static_cast<std::remove_reference_t<T>*>(value);
    } else {
        return static_cast<T>(value);
    }
}

template<class T>
F4SEValue Encode(T value)
{
    F4SEValue result{Tag<T>(), {}};
    if constexpr (std::is_enum_v<T>) {
        result = Encode(static_cast<std::underlying_type_t<T>>(value));
    } else if constexpr (std::is_pointer_v<T>) {
        result.pointer = value;
    } else if constexpr (std::is_same_v<T, bool>) {
        result.boolean = value;
    } else if constexpr (std::is_same_v<T, int>) {
        result.integer = value;
    } else if constexpr (std::is_same_v<T, unsigned int>) {
        result.unsignedInteger = value;
    } else if constexpr (std::is_same_v<T, float>) {
        result.number = value;
    } else {
        static_assert(sizeof(T) == 0, "Unsupported binding result");
    }
    return result;
}

template<class... Args>
struct CallSignature {
    std::array<ffi_type*, 1 + sizeof...(Args)> types{&ffi_type_pointer, FfiType<Args>()...};
    ffi_cif cif{};
    ffi_status status = ffi_prep_cif(&cif, FFI_DEFAULT_ABI, static_cast<unsigned>(types.size()), &ffi_type_void, types.data());
};

template<class... Args>
F4SEValue InvokeThunk(std::span<const F4SEValue> arguments, void (*thunk)(F4SEValue*, Wire<Args>...) noexcept)
{
    constexpr std::array<ValueType, sizeof...(Args)> tags{Tag<Args>()...};
    constexpr std::array<bool, sizeof...(Args)> required{std::is_reference_v<Args>...};
    if (arguments.size() != tags.size()) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    F4SEValue result{};
    auto* output = &result;
    std::array<void*, 1 + sizeof...(Args)> slots{&output};
    for (std::size_t index = 0; index < arguments.size(); ++index) {
        const auto& value = arguments[index];
        if (value.type != tags[index] || (required[index] && !value.pointer)) {
            return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
        }
        slots[index + 1] = const_cast<void*>(static_cast<const void*>(&value.storage));
    }
    // ffi_cif retains the address of its argument-type array.
    static CallSignature<Args...> signature;
    if (signature.status != FFI_OK) {
        return {ValueType::Error, {.error = InvokeError::RegistryUnavailable}};
    }
    ffi_call(&signature.cif, FFI_FN(thunk), nullptr, slots.data());
    return result;
}

template<auto Method, class... Args>
F4SEValue Call(std::span<const F4SEValue> arguments) noexcept
{
    return InvokeThunk<Args...>(arguments, +[](F4SEValue* result, Wire<Args>... values) noexcept {
        try {
            if constexpr (std::is_void_v<std::invoke_result_t<decltype(Method), Args...>>) {
                std::invoke(Method, Decode<Args>(values)...);
                *result = {ValueType::Void, {}};
            } else {
                *result = Encode(std::invoke(Method, Decode<Args>(values)...));
            }
        } catch (...) {
            *result = {ValueType::Error, {.error = InvokeError::NativeException}};
        }
    });
}

}
