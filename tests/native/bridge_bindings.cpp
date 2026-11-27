#include "method_registry.h"
#include "events.h"
#include "AutoEventSinks.h"
#include <RE/N/NiPoint3.h>

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

F4SEValue GetTestPoint(std::span<const F4SEValue> arguments) noexcept
{
    if (!arguments.empty()) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    static constexpr RE::NiPoint3 point{3.0f, 4.0f, 0.0f};
    return {ValueType::Pointer, {.pointer = &point}};
}

F4SEValue EmitObjectLoaded(std::span<const F4SEValue> arguments) noexcept
{
    if (!arguments.empty()) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    // Exercise the production sink without resolving a game singleton outside Fallout.
    RE::BSTEventSource<RE::TESObjectLoadedEvent> source;
    const RE::TESObjectLoadedEvent event{0xFEDC1234u, true};
    auto& sink = CommonLibEventSink<RE::TESObjectLoadedEvent>::Instance();
    const auto control = sink.ProcessEvent(event, &source);
    return {ValueType::Bool, {.boolean = control == RE::BSEventNotifyControl::kContinue}};
}

}

void RegisterHostMethods(MethodRegistry& registry)
{
    registry.Register("Test_Add", TestAdd);
    registry.Register("Test_EmitMessage", EmitTestMessage);
    registry.Register("Test_GetPoint", GetTestPoint);
    registry.Register("Test_EmitObjectLoaded", EmitObjectLoaded);
}

}
