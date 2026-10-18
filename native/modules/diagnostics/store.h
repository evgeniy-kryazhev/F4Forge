#pragma once

#include "channel.h"
#include <deque>
#include <span>
#include <optional>
#include <stdexcept>

namespace f4forge::diagnostics {

class Store final {
public:
    using ErrorSink = std::function<void(std::string_view)>;
    static constexpr std::size_t Capacity = 256;

    void Record(const DataValue& operation);
    DataValue Operations(std::string_view modId, std::size_t maximum) const;
    void Hook(std::string name, std::string kind, std::string state, std::string detail = {});
    DataValue Hooks() const;
    void SetErrorSink(ErrorSink sink);

private:
    void Report(std::string_view text) const noexcept;
    mutable std::mutex gate_;
    std::deque<DataValue> operations_;
    std::map<std::string, DataValue, std::less<>> hooks_;
    ErrorSink errorSink_;
};

Store& Global();

class Service final {
public:
    Service(Channel& channel, Store& store);
    ~Service();
    Service(const Service&) = delete;
    Service& operator=(const Service&) = delete;

private:
    Channel& channel_;
    std::vector<std::uint64_t> registrations_;
};

// These checks validate memory access and optional byte patterns, not game
// semantics. A resolved Address Library ID alone does not validate an ABI.
bool MatchPattern(std::span<const std::uint8_t> bytes, std::span<const std::optional<std::uint8_t>> pattern);
void CheckCode(std::uintptr_t address);
std::uintptr_t CheckVtableSlot(std::uintptr_t table, std::size_t index);
void CheckReadable(std::uintptr_t address, std::size_t size);

template<class Install>
void CheckAndInstall(std::string_view name, std::string_view kind, Install&& install)
{
    Global().Hook(std::string(name), std::string(kind), "checking");
    try {
        install();
        Global().Hook(std::string(name), std::string(kind), "active");
    } catch (const std::exception& exception) {
        Global().Hook(std::string(name), std::string(kind), "failed", exception.what());
        throw;
    } catch (...) {
        Global().Hook(std::string(name), std::string(kind), "failed", "Unknown hook installation failure.");
        throw;
    }
}

}
