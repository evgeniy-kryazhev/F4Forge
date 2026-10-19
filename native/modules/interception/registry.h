#pragma once

#include "channel.h"

namespace f4forge::interception {

class Registry final {
public:
    using Validator = std::function<DataValue(const DataValue&, const DataValue&)>;
    using ErrorSink = std::function<void(std::string_view)>;

    Registry(Channel& channel, ErrorSink report);
    ~Registry();
    Registry(const Registry&) = delete;
    Registry& operator=(const Registry&) = delete;

    // The caller supplies feature-specific validation. The shared registry knows
    // no console, camera, or engine layouts and never exposes native pointers.
    DataValue Apply(std::string_view name, DataValue input, const Validator& validate);

private:
    struct Entry {
        std::string name;
        std::string method;
        std::atomic<bool> active = true;
    };

    Channel& channel_;
    ErrorSink report_;
    std::mutex gate_;
    std::map<std::uint64_t, std::shared_ptr<Entry>> entries_;
    std::uint64_t next_ = 1;
    std::vector<std::uint64_t> handlers_;
};

Registry* Active() noexcept;

}
