#pragma once

#include "channel.h"
#include <vector>

namespace f4forge::forms {

// Thread ownership and session identity are independent of engine layouts, so
// stale references and off-thread calls can be tested without launching the game.
class Session final {
public:
    using IsGameThread = std::function<bool()>;

    explicit Session(IsGameThread isGameThread);
    void Ready(bool ready);
    void Reset();
    std::uint64_t Require(std::uint64_t expected = 0) const;

private:
    mutable std::mutex gate_;
    IsGameThread isGameThread_;
    bool ready_ = false;
    std::uint64_t generation_ = 1;
};

enum class ValueKind { Current, Base, Permanent };

struct Backend {
    std::function<DataValue(std::uint32_t)> describe;
    std::function<DataValue(const DataValue&)> resolve;
    std::function<DataValue()> player;
    std::function<DataValue(std::uint32_t)> reference;
    std::function<float(std::uint32_t, std::uint32_t, ValueKind)> actorValue;
    std::function<void(std::uint32_t, std::uint32_t, float)> setBaseValue;
};

class Service final {
public:
    Service(Channel& channel, Session& session, Backend backend);
    ~Service();
    Service(const Service&) = delete;
    Service& operator=(const Service&) = delete;

private:
    void Add(std::string name, Channel::Handler handler);
    DataValue Describe(DataValue value, std::uint64_t generation) const;
    Channel& channel_;
    Session& session_;
    Backend backend_;
    std::vector<std::uint64_t> registrations_;
};

}
