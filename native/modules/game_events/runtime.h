#pragma once

#include "channel.h"
#include <condition_variable>

namespace f4forge::game_events {

// Numeric values are checked against F4SE in module.cpp. No engine types escape the adapters.
enum class MessageType : std::uint32_t {
    PreLoadGame = 2,
    PostLoadGame = 3,
    PreSaveGame = 4,
    PostSaveGame = 5,
    InputLoaded = 7,
    NewGame = 8,
    GameLoaded = 9,
    GameDataReady = 10
};

struct Message {
    std::uint32_t type;
    const void* data;
    std::uint32_t size;

    bool Is(MessageType expected) const noexcept { return type == static_cast<std::uint32_t>(expected); }
};

class Runtime;

struct Adapter {
    std::string name;
    std::function<void(Runtime&, const Message&)> message;
    std::function<void()> stop;
    int order = 0;
};

class AdapterRegistration final {
public:
    explicit AdapterRegistration(Adapter adapter);
};

class Runtime final {
public:
    using ErrorSink = std::function<void(std::string_view)>;

    void Start(Channel& channel, ErrorSink reportError);
    void Stop() noexcept;
    void Dispatch(const Message& message) noexcept;
    bool DataReady() const noexcept { return dataReady_.load(); }

    // Engine callbacks take a lease. Stop disables new leases and drains existing ones
    // before detaching sources or releasing the channel. No mutex spans mod code.
    template<class Callback>
    void Invoke(Callback&& callback) noexcept
    {
        bool entered = false;
        try {
            entered = Enter();
            if (entered) {
                callback();
            }
        } catch (const std::exception& exception) {
            Report(exception.what());
        } catch (...) {
            Report("Unknown game event adapter failure.");
        }
        if (entered) {
            Leave();
        }
    }

    // Only call while holding an Invoke lease (Dispatch also supplies one).
    void Publish(std::string_view name, const DataValue& value);
    void Report(std::string_view message) const noexcept;

private:
    bool Enter();
    void Leave() noexcept;

    std::mutex gate_;
    std::condition_variable idle_;
    bool accepting_ = false;
    std::size_t active_ = 0;
    std::atomic<bool> dataReady_ = false;
    Channel* channel_ = nullptr;
    ErrorSink reportError_;
    std::vector<Adapter> adapters_;
};

// Process-lifetime instance: F4SE has no listener unregistration API.
Runtime& Host();

}
