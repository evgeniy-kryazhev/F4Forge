#pragma once

#include "events.h"
#include <RE/B/BSTEvent.h>
#include <array>
#include <bit>

namespace f4forge {

// Slots live on the notifying thread's stack until the managed callback returns.
struct EventSnapshot {
    const std::uint64_t* fields;
    std::int32_t count;
};

template <class Event>
struct EventBinding;

template <class Event>
void PublishCommonLibEvent(const Event& event, const void* sender)
{
    const auto fields = EventBinding<Event>::Snapshot(event);
    const EventSnapshot snapshot{fields.data(), static_cast<std::int32_t>(fields.size())};
    const F4SEValue payload{ValueType::EventSnapshot, {.pointer = &snapshot}};
    PublishEvent(EventBinding<Event>::Name, sender, payload);
}

template <class Event>
class CommonLibEventSink final : public RE::BSTEventSink<Event> {
public:
    static CommonLibEventSink& Instance()
    {
        static CommonLibEventSink sink;
        return sink;
    }

    RE::BSEventNotifyControl ProcessEvent(const Event& event, RE::BSTEventSource<Event>* source) override
    {
        PublishCommonLibEvent(event, source);
        return RE::BSEventNotifyControl::kContinue;
    }
};

}
