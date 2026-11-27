#include "method_registry.h"
#include "events.h"
#include "AutoEventSinks.h"
#include <RE/N/NiPoint3.h>

#include <limits>
#include <new>

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

template <class Event>
Event& InputSample()
{
    // BSFixedString destruction calls the game's string pool even for a null string.
    // Keep one sample per type for the test process lifetime, outside the game.
    alignas(Event) static std::byte storage[sizeof(Event)];
    static Event* sample = ::new (storage) Event{};
    return *sample;
}

F4SEValue EmitInput(std::span<const F4SEValue> arguments) noexcept
{
    if (!arguments.empty()) {
        return {ValueType::Error, {.error = InvokeError::InvalidArguments}};
    }
    CommonLibInputHandler handler;
    auto& button = InputSample<RE::ButtonEvent>();
    button.eventType = RE::INPUT_EVENT_TYPE::kNone;
    if (handler.ShouldHandleEvent(nullptr) || handler.ShouldHandleEvent(&button)) {
        return {ValueType::Bool, {.boolean = false}};
    }
    handler.OnButtonEvent(nullptr);
    button.eventType = button.TYPE;
    button.device = RE::INPUT_DEVICE::kKeyboard;
    button.idCode = 30;
    button.value = 1.0f;
    button.heldDownSecs = 0.0f;

    auto& character = InputSample<RE::CharacterEvent>();
    character.eventType = character.TYPE;
    character.charCode = 0x0416;
    auto& mouse = InputSample<RE::MouseMoveEvent>();
    mouse.eventType = mouse.TYPE;
    mouse.mouseInputX = -7;
    mouse.mouseInputY = 3;
    auto& cursor = InputSample<RE::CursorMoveEvent>();
    cursor.eventType = cursor.TYPE;
    cursor.cursorPosX = 320;
    cursor.cursorPosY = 240;
    if (!handler.ShouldHandleEvent(&button) || !handler.ShouldHandleEvent(&character)
        || !handler.ShouldHandleEvent(&mouse) || !handler.ShouldHandleEvent(&cursor)) {
        return {ValueType::Bool, {.boolean = false}};
    }
    handler.OnButtonEvent(&button);
    button.device = RE::INPUT_DEVICE::kMouse;
    button.idCode = 1;
    button.value = 0.0f;
    button.heldDownSecs = 0.25f;
    handler.OnButtonEvent(&button);
    handler.OnCharacterEvent(&character);
    handler.OnMouseMoveEvent(&mouse);
    handler.OnCursorMoveEvent(&cursor);
    return {ValueType::Bool, {.boolean = true}};
}

}

void RegisterHostMethods(MethodRegistry& registry)
{
    registry.Register("Test_Add", TestAdd);
    registry.Register("Test_EmitMessage", EmitTestMessage);
    registry.Register("Test_GetPoint", GetTestPoint);
    registry.Register("Test_EmitObjectLoaded", EmitObjectLoaded);
    registry.Register("Test_EmitInput", EmitInput);
}

}
