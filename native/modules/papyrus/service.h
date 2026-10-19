#pragma once

#include "channel.h"
#include <set>

namespace f4forge::papyrus
{

using Completion = std::function<void(DataValue value, std::string error)>;
using IsPending = std::function<bool()>;

// Queue and VM callbacks contain copied data and native state, never SDK pointers.
struct Backend
{
    std::function<void(const DataValue&, Completion, IsPending)> submit;
    std::function<void(std::string_view)> release;
    std::function<void()> reset;
};

class Service final
{
  public:
    using Publish = std::function<void(const DataValue&)>;
    Service(Channel& channel, Backend backend, Publish publish);
    ~Service();
    void Stop();
    void Reset();
    Service(const Service&) = delete;
    Service& operator=(const Service&) = delete;

  private:
    struct State;
    Channel& channel_;
    std::shared_ptr<State> state_;
    std::vector<std::uint64_t> registrations_;
};

} // namespace f4forge::papyrus
