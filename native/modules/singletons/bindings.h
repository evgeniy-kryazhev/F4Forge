#pragma once

#include "channel.h"
#include <cmath>
#include <concepts>
#include <limits>
#include <stdexcept>
#include <type_traits>
#include <utility>
#include <vector>

namespace f4forge::singletons {

// Registration owns callback state; removal completes before this owner is destroyed.
class Bindings final {
public:
    Bindings(Channel& channel, std::function<bool()> isGameThread);
    ~Bindings();
    Bindings(const Bindings&) = delete;
    Bindings& operator=(const Bindings&) = delete;

    void Add(std::string name, Channel::Handler handler);

private:
    Channel& channel_;
    std::function<bool()> isGameThread_;
    std::vector<std::uint64_t> registrations_;
};

template <class T>
T* RequireSingleton(T* singleton, std::string_view name)
{
    if (singleton == nullptr) {
        throw std::runtime_error("Engine singleton is unavailable: " + std::string(name));
    }
    return singleton;
}

// nlohmann's numeric conversions may narrow silently; reject invalid wire values first.
template <class T>
T ReadArgument(const DataValue& value)
{
    if constexpr (std::is_enum_v<T>) {
        return static_cast<T>(ReadArgument<std::underlying_type_t<T>>(value));
    } else {
        if constexpr (std::same_as<T, bool>) {
            if (!value.is_boolean()) {
                throw std::invalid_argument("A boolean singleton argument is required.");
            }
        } else if constexpr (std::integral<T>) {
            if (value.is_number_unsigned()) {
                if (value.get<std::uint64_t>() > static_cast<std::uint64_t>(std::numeric_limits<T>::max())) {
                    throw std::out_of_range("Singleton integer argument is out of range.");
                }
            } else if (value.is_number_integer()) {
                if (!std::in_range<T>(value.get<std::int64_t>())) {
                    throw std::out_of_range("Singleton integer argument is out of range.");
                }
            } else {
                throw std::invalid_argument("An integer singleton argument is required.");
            }
        } else if constexpr (std::floating_point<T>) {
            if (!value.is_number()) {
                throw std::invalid_argument("A numeric singleton argument is required.");
            }
            const auto number = value.get<double>();
            if (!std::isfinite(number) || std::abs(number) > std::numeric_limits<T>::max()) {
                throw std::out_of_range("Singleton floating-point argument is out of range.");
            }
        } else if constexpr (std::same_as<T, std::string>) {
            if (!value.is_string()) {
                throw std::invalid_argument("A string singleton argument is required.");
            }
            const auto& text = value.get_ref<const std::string&>();
            if (text.find('\0') != std::string::npos) {
                throw std::invalid_argument("Native strings cannot contain NUL characters.");
            }
        }
        return value.get<T>();
    }
}

void RegisterGenerated(Bindings& bindings);

}
