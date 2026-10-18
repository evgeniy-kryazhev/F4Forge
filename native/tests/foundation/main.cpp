#include "../../modules/diagnostics/store.h"
#include "../../modules/forms/service.h"
#include "../../modules/console/router.h"
#include <array>
#include <atomic>
#include <future>
#include <iostream>
#include <thread>

namespace {

using f4forge::DataValue;

class BinarySerializer final : public f4forge::IDataSerializer {
public:
    std::string_view Format() const noexcept override { return "test.binary"; }

    std::string Serialize(const DataValue& value) const override
    {
        return std::string("\0\xff", 2) + json_->Serialize(value);
    }

    DataValue Deserialize(std::string_view bytes) const override
    {
        if (bytes.size() < 2 || bytes[0] != '\0' || static_cast<unsigned char>(bytes[1]) != 255) {
            throw std::invalid_argument("Missing binary prefix.");
        }
        return json_->Deserialize(bytes.substr(2));
    }

private:
    std::shared_ptr<const f4forge::IDataSerializer> json_ = f4forge::CreateDataSerializer();
};

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

template<class Action>
bool Throws(Action action)
{
    try {
        action();
        return false;
    } catch (const std::exception&) {
        return true;
    }
}

DataValue Call(f4forge::Channel& channel, std::string method, DataValue arguments)
{
    const auto& serializer = channel.Serializer();
    return serializer.Deserialize(channel.Call(serializer.Serialize({{"method", method}, {"arguments", arguments}})));
}

DataValue Record(std::string owner, std::string name, bool success = true)
{
    return {{"modId", owner}, {"name", name}, {"id", "operation"}, {"parentId", "parent"},
        {"kind", "message.call"}, {"elapsedMilliseconds", 1.5}, {"success", success},
        {"exceptionType", "ExampleError"}, {"message", "Ошибка 🌍"}, {"stackTrace", "Example stack"}};
}

void Diagnostics(std::shared_ptr<const f4forge::IDataSerializer> serializer)
{
    f4forge::Channel channel(std::move(serializer));
    f4forge::diagnostics::Store store;
    int failures = 0;
    store.SetErrorSink([&](std::string_view text) {
        ++failures;
        Require(text.find("Mod A") != std::string_view::npos, "Missing diagnostic owner");
        Require(!store.Operations("Mod A", 1).empty(), "Sink ran before record was visible");
        throw std::runtime_error("Logger failed");
    });
    {
        f4forge::diagnostics::Service service(channel, store);
        Require(Call(channel, "diagnostics.record", Record("Mod A", "nested", false)).at("value") == true, "Logger failure escaped diagnostics");
        Require(failures == 1, "Failure was not logged");
        store.SetErrorSink({});
        for (int index = 0; index < 300; ++index) {
            store.Record(Record(index % 2 == 0 ? "Mod A" : "Mod B", std::to_string(index)));
        }
        Require(store.Operations("", 256).size() == 256, "Diagnostic history is not bounded");
        Require(store.Operations("Mod A", 1).at(0).at("name") == "298", "Owner filter or newest-first ordering failed");
        Require(Throws([&] { store.Operations("", 0); }), "Invalid limit was accepted");
        auto invalid = Record("", "test");
        Require(Call(channel, "diagnostics.record", invalid).at("success") == false, "Empty owner was accepted");
        auto large = Record("Large", "stack");
        large["stackTrace"] = std::string(32000, '\n');
        for (int index = 0; index < 80; ++index) {
            store.Record(large);
        }
        const auto history = Call(channel, "diagnostics.operations", {{"modId", "Large"}, {"maximum", 256}});
        Require(history.at("success") == true && !history.at("value").empty() && history.at("value").size() < 80, "Large history did not fit transport limits");
        store.Hook("example", "vtable", "failed", "Not executable");
        Require(Call(channel, "diagnostics.hooks", nullptr).at("value").at(0).at("state") == "failed", "Hook status was lost");
    }
    Require(Call(channel, "diagnostics.hooks", nullptr).at("error").at("code") == "method_not_found", "Diagnostic shutdown leaked handlers");
    f4forge::diagnostics::Service restarted(channel, store);
    Require(Call(channel, "diagnostics.hooks", nullptr).at("success") == true, "Diagnostics could not restart");
}

__declspec(noinline) void Executable()
{
    std::atomic_signal_fence(std::memory_order_seq_cst);
}

void HookChecks()
{
    using namespace f4forge::diagnostics;
    const std::array<std::uint8_t, 3> bytes{0x48, 0x89, 0x01};
    const std::array<std::optional<std::uint8_t>, 3> good{std::uint8_t{0x48}, std::nullopt, std::uint8_t{0x01}};
    const std::array<std::optional<std::uint8_t>, 1> bad{std::uint8_t{0xff}};
    Require(MatchPattern(bytes, good) && !MatchPattern(bytes, bad), "Wildcard pattern check failed");
    Require(!MatchPattern(std::span(bytes).first(1), good), "Short byte range was accepted");
    Require(!MatchPattern(bytes, {}), "Empty signature was accepted");
    CheckReadable(reinterpret_cast<std::uintptr_t>(bytes.data()), bytes.size());
    Require(Throws([] { CheckReadable(0, 8); }), "Null hook memory was accepted");
    Require(Throws([&] { CheckCode(reinterpret_cast<std::uintptr_t>(bytes.data())); }), "Data memory was accepted as code");
    const auto code = reinterpret_cast<std::uintptr_t>(&Executable);
    CheckCode(code);
    const std::array<std::uintptr_t, 2> table{code, code};
    Require(CheckVtableSlot(reinterpret_cast<std::uintptr_t>(table.data()), 1) == code, "Valid vtable slot was rejected");
    Require(Throws([&] { CheckVtableSlot(reinterpret_cast<std::uintptr_t>(table.data()) + 1, 0); }), "Unaligned table was accepted");
    const std::array<std::uintptr_t, 1> nullTable{0};
    Require(Throws([&] { CheckVtableSlot(reinterpret_cast<std::uintptr_t>(nullTable.data()), 0); }), "Null original function was accepted");
    bool installed = false;
    Require(Throws([&] {
        CheckAndInstall("test.failed", "function", [&] {
            CheckCode(0);
            installed = true;
        });
    }), "Bad hook did not fail");
    Require(!installed, "Hook installed after validation failure");
    CheckAndInstall("test.active", "function", [&] { installed = true; });
    Require(installed, "Valid hook did not install");
}

void Objects(std::shared_ptr<const f4forge::IDataSerializer> serializer)
{
    f4forge::Channel channel(std::move(serializer));
    const auto gameThread = std::this_thread::get_id();
    f4forge::forms::Session session([gameThread] {
        return std::this_thread::get_id() == gameThread;
    });
    float base = 50;
    f4forge::forms::Backend backend;
    backend.describe = [](std::uint32_t id) -> DataValue {
        return id == 0 ? DataValue(nullptr) : DataValue{{"formId", id}, {"type", "ACHR"}};
    };
    backend.resolve = [&](const DataValue&) { return backend.describe(0x14); };
    backend.player = [&] { return backend.describe(0x14); };
    backend.reference = [](std::uint32_t id) { return DataValue{{"baseFormId", id}, {"cellFormId", 0}}; };
    backend.actorValue = [&](std::uint32_t id, std::uint32_t valueId, f4forge::forms::ValueKind kind) {
        Require(id == 0x14 && valueId == 0x2d4, "Actor identities were changed");
        return kind == f4forge::forms::ValueKind::Base ? base : 100.0F;
    };
    backend.setBaseValue = [&](std::uint32_t, std::uint32_t, float value) { base = value; };
    {
        f4forge::forms::Service service(channel, session, backend);
        Require(Call(channel, "forms.player", nullptr).at("success") == false, "Objects were accessible before readiness");
        // F4SE can deliver load completion on a loader thread. It must not
        // change which thread is allowed to execute console object operations.
        std::async(std::launch::async, [&] { session.Ready(true); }).get();
        const auto player = Call(channel, "forms.player", nullptr).at("value");
        const auto generation = player.at("session").get<std::uint64_t>();
        Require(player.at("formId") == 0x14 && generation != 0, "Session was not attached to object");
        std::vector<std::string> output;
        f4forge::console::Router router(channel, [&](std::string_view line) {
            output.emplace_back(line);
        }, [](std::string_view) { return false; });
        const auto command = channel.Add("console.command.hlog", [&](const DataValue&) -> DataValue {
            const auto resolved = Call(channel, "forms.player", nullptr);
            Require(resolved.at("success") == true, "Console could not resolve the player after background loading");
            const auto snapshot = Call(channel, "forms.reference", resolved.at("value"));
            Require(snapshot.at("success") == true, "Console could not read the player snapshot");
            return {{"lines", {"Player snapshot read"}}};
        });
        Require(router.Execute("hlog", 0), "Registered object command was not handled");
        Require(output == std::vector<std::string>{"Player snapshot read"}, "Console object command failed");
        channel.Remove(command);
        Require(Call(channel, "forms.describe", {{"formId", 0}}).at("value").is_null(), "Null form did not remain null");
        Require(Call(channel, "forms.describe", {{"formId", -1}}).at("success") == false, "Negative form ID was accepted");
        Require(Call(channel, "forms.describe", {{"formId", 0x100000000ULL}}).at("success") == false, "Overflowed form ID was accepted");
        Require(Call(channel, "forms.describe", {{"formId", 1.5}}).at("success") == false, "Fractional form ID was accepted");
        Require(Call(channel, "forms.reference", {{"formId", 0x14}, {"session", 0}}).at("success") == false, "Unbound reference was accepted");
        Require(Call(channel, "forms.resolve", {{"plugin", "Example.esp"}, {"localId", 0x100000000ULL}}).at("success") == false, "Overflowed local ID was accepted");
        auto arguments = DataValue{{"formId", 0x14}, {"session", generation}, {"actorValueFormId", 0x2d4},
            {"actorValueSession", generation}, {"kind", "base"}, {"value", 75.0}};
        Require(Call(channel, "forms.actor_value", arguments).at("value") == 50, "Base value query failed");
        Require(Call(channel, "forms.set_base_value", arguments).at("value") == true && base == 75, "Generic value mutation failed");
        arguments["kind"] = "invalid";
        Require(Call(channel, "forms.actor_value", arguments).at("success") == false, "Unknown actor value kind was accepted");
        Require(std::async(std::launch::async, [&] { return Throws([&] { session.Require(); }); }).get(), "Off-thread access was allowed");
        const auto workerResult = std::async(std::launch::async, [&] {
            session.Ready(true);
            return Call(channel, "forms.player", nullptr);
        }).get();
        Require(workerResult.at("success") == false, "Load notification granted a worker access to objects");
        Require(Call(channel, "forms.player", nullptr).at("success") == true, "Load notification revoked game-thread access");
        session.Reset();
        Require(Call(channel, "forms.reference", arguments).at("success") == false, "Expired session still accessed the backend");
        session.Ready(false);
        Require(Call(channel, "forms.player", nullptr).at("success") == false, "Objects were accessible during loading");
    }
    Require(Call(channel, "forms.player", nullptr).at("error").at("code") == "method_not_found", "Objects service leaked handlers");
    f4forge::forms::Service restarted(channel, session, backend);
    session.Ready(true);
    Require(Call(channel, "forms.player", nullptr).at("success") == true, "Objects service could not restart");
}

void RegistrationRollback()
{
    f4forge::Channel channel;
    f4forge::diagnostics::Store store;
    const auto duplicate = channel.Add("diagnostics.operations", [](const DataValue&) { return true; });
    Require(Throws([&] { f4forge::diagnostics::Service service(channel, store); }), "Duplicate diagnostic service was accepted");
    Require(Call(channel, "diagnostics.record", nullptr).at("error").at("code") == "method_not_found", "Partial registration leaked after failure");
    Require(Call(channel, "diagnostics.operations", nullptr).at("value") == true, "Rollback removed another owner's handler");
    channel.Remove(duplicate);
}

}

int main()
{
    try {
        Diagnostics(f4forge::CreateDataSerializer());
        Diagnostics(std::make_shared<BinarySerializer>());
        HookChecks();
        Objects(f4forge::CreateDataSerializer());
        Objects(std::make_shared<BinarySerializer>());
        RegistrationRollback();
        std::cout << "Diagnostics, bounded history, hook validation, game objects and session guards passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
